using System.Text.RegularExpressions;
using Genesis.Core.Pipeline;
using Genesis.Core.Spec;
using Genesis.Core.Util;

namespace Genesis.Core.Stages;

/// <summary>
/// Stage 6 `emit controllers+views`.
///
/// Three controller provenances, matching how a student would really mix techniques:
///   generatedBy=scaffold            -> aspnet-codegenerator controller, THEN declarative
///                                      deviations applied (removedActions, redirects, sorting)
///   generatedBy=template            -> Scriban emission (SummaryController, HomeController...)
///   identity.applicationUser.controller (writtenFromScratch) -> dedicated template with the
///                                      AdminCannotDeleteSelf rule enforced SERVER-SIDE.
///
/// Deviations are applied as targeted source transforms on the scaffolded C# - this is
/// deliberately regex-on-known-scaffold-shapes, NOT a Roslyn rewrite: the scaffolder
/// output for a given SDK pin is stable, and the transforms fail loud when a pattern
/// is not found (so an SDK bump surfaces immediately instead of silently emitting
/// wrong code). ADR-006 records this trade-off.
/// </summary>
public sealed class EmitControllersViewsStage : IStage
{
    public string Name => "emit-controllers-views";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        // ImageService + partials first - views reference them.
        if (spec.Models.Any(m => m.Images is not null))
        {
            await EmitModelsStage.WriteAsync(ctx, "Services/ImageService.cs",
                engine.RenderFile(ctx.Template("controllers/image_service.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Views/Shared/_ImageUploadPartial.cshtml",
                engine.RenderFile(ctx.Template("views/image_upload_partial.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Views/Shared/_ImageGalleryPartial.cshtml",
                engine.RenderFile(ctx.Template("views/image_gallery_partial.sbn")), ct);
            // Task 14: per-model Admin image-management controller + view (template-generated,
            // not scaffold-based — avoids fragile scaffold transforms on Create/Edit actions).
            foreach (var imgModel in spec.Models.Where(m => m.Images is not null))
            {
                var extra = new { model = imgModel };
                await EmitModelsStage.WriteAsync(ctx,
                    $"Areas/Admin/Controllers/{imgModel.Name}ImagesController.cs",
                    engine.RenderFile(ctx.Template("controllers/product_images_controller.sbn"), extra), ct);
                await EmitModelsStage.WriteAsync(ctx,
                    $"Areas/Admin/Views/{imgModel.Name}Images/Index.cshtml",
                    engine.RenderFile(ctx.Template("views/admin_product_images.sbn"), extra), ct);
            }
        }

        // Task 15/16/10 (Package E): shared single-file upload helper (FileUploadService) - distinct from
        // the per-model ImageService. Emitted whenever a consumer exists: admin pfp upload
        // (ApplicationUser.ProfilePicturePath), review image upload (rating feature), or post image
        // upload (posts feature). Must stay in sync with program.sbn's DI registration condition
        // (has_pfp || features.rating || features.posts).
        var hasPfp = spec.Identity?.ApplicationUser.ExtraProperties.Any(p => p.Name == "ProfilePicturePath") ?? false;
        if (hasPfp || FeatureEnabled(spec, "rating") || FeatureEnabled(spec, "posts"))
            await EmitModelsStage.WriteAsync(ctx, "Services/FileUploadService.cs",
                engine.RenderFile(ctx.Template("controllers/file_upload_service.sbn")), ct);

        // Stale declared views from a previous failed run can break the scaffolder's
        // internal build - remove them; they are re-emitted at the end of this stage.
        if (spec.Views is { } vPre)
            foreach (var pagePre in vPre.Pages)
            {
                var stale = Path.Combine(ctx.ProjectDir, pagePre.File);
                if (File.Exists(stale)) File.Delete(stale);
            }

        foreach (var controller in spec.Controllers)
        {
            switch (controller.GeneratedBy)
            {
                case "scaffold":
                    await ScaffoldControllerAsync(ctx, controller, ct);
                    if (controller.Area is { } scaffoldArea)
                        InjectAreaAttribute(ctx, controller, scaffoldArea);
                    InjectImageInclude(ctx, controller);
                    ApplySoftDelete(ctx, controller);
                    ApplyBgResiduePass(ctx, controller);
                    StripSystemFieldsFromViews(ctx, controller);
                    InjectReviewLoadingIntoDetails(ctx, controller);
                    ApplyDeviations(ctx, controller, engine);
                    break;
                case "template":
                    await EmitModelsStage.WriteAsync(ctx, controller.File,
                        engine.RenderFile(ctx.Template("controllers/controller.sbn"), new { controller }), ct);
                    break;
            }
        }

        // Written-from-scratch UsersController + its Index view (template-generated pair).
        if (spec.Identity?.ApplicationUser.Controller is { WrittenFromScratch: true } uc)
        {
            await EmitModelsStage.WriteAsync(ctx, uc.File,
                engine.RenderFile(ctx.Template("controllers/users_controller.sbn"), new { uc }), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Admin/Views/Users/Index.cshtml",
                engine.RenderFile(ctx.Template("views/users_index.sbn")), ct);
            // Task 15: the Edit action previously returned View(user) with NO emitted view (a latent
            // 500). Emit the admin user Edit form (FirstName + profile-picture upload when present).
            await EmitModelsStage.WriteAsync(ctx, "Areas/Admin/Views/Users/Edit.cshtml",
                engine.RenderFile(ctx.Template("views/users_edit.sbn")), ct);
            // Task 7: admin user Details page (aggregation via the shared UserProfileService). The
            // controller only exposes Details when posts||catalogues (the service exists), so gate the
            // view the same way to avoid emitting a view for a non-existent action.
            if (FeatureEnabled(spec, "posts") || FeatureEnabled(spec, "catalogues"))
                await EmitModelsStage.WriteAsync(ctx, "Areas/Admin/Views/Users/Details.cshtml",
                    engine.RenderFile(ctx.Template("views/users_details.sbn")), ct);
        }

        // Feature controller/view pairs - each enabled feature emits its full stack.
        if (FeatureEnabled(spec, "posts"))
        {
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Controllers/PostsController.cs",
                engine.RenderFile(ctx.Template("controllers/posts_controller.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Posts/Index.cshtml",
                engine.RenderFile(ctx.Template("views/posts_index.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Posts/Details.cshtml",
                engine.RenderFile(ctx.Template("views/posts_details.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Posts/Create.cshtml",
                engine.RenderFile(ctx.Template("views/posts_create.sbn")), ct);
        }
        if (FeatureEnabled(spec, "orders"))
        {
            var primary = FeaturePrimaryModel(spec, "orders");
            var priceProp = spec.Models.First(m => m.Name == primary).Properties
                .FirstOrDefault(pp => pp.Type == "decimal")?.Name ?? "Price";
            var extra = new { primary, price_property = priceProp };
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Controllers/CartController.cs",
                engine.RenderFile(ctx.Template("controllers/cart_controller.sbn"), extra), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Cart/Index.cshtml",
                engine.RenderFile(ctx.Template("views/cart_index.sbn"), extra), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Cart/Orders.cshtml",
                engine.RenderFile(ctx.Template("views/cart_orders.sbn"), extra), ct);
            // A6: per-order details (read-only, own orders only).
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Cart/OrderDetails.cshtml",
                engine.RenderFile(ctx.Template("views/cart_order_details.sbn"), extra), ct);
        }
        if (FeatureEnabled(spec, "follows"))
        {
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Controllers/FollowsController.cs",
                engine.RenderFile(ctx.Template("controllers/follows_controller.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Follows/List.cshtml",
                engine.RenderFile(ctx.Template("views/follows_list.sbn")), ct);
        }
        if (FeatureEnabled(spec, "rating"))
        {
            var primary = FeaturePrimaryModel(spec, "rating");
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Controllers/ReviewsController.cs",
                engine.RenderFile(ctx.Template("controllers/reviews_controller.sbn"), new { primary }), ct);
            // A15: NO standalone Отзиви page - feedback lives only under its product's Details.
            // A3/A14: emit the rich product Details (carousel + props + feedback) into the
            // Client Products view folder, replacing the scaffolded Details.
            await EmitModelsStage.WriteAsync(ctx, $"Areas/Client/Views/{primary}s/Details.cshtml",
                engine.RenderFile(ctx.Template("views/product_details.sbn"), new { primary }), ct);
        }

        // Catalogues feature: per-user named lists over the configured primary model.
        if (FeatureEnabled(spec, "catalogues"))
        {
            var primary = CataloguesPrimaryModel(spec);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Controllers/CataloguesController.cs",
                engine.RenderFile(ctx.Template("controllers/catalogues_controller.sbn"), new { primary }), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Catalogues/Index.cshtml",
                engine.RenderFile(ctx.Template("views/catalogues_index.sbn"), new { primary }), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Catalogues/_PickerPartial.cshtml",
                engine.RenderFile(ctx.Template("views/catalogues_picker_partial.sbn"), new { primary }), ct);
            // A5: public (anonymous) catalogue browsing.
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Catalogues/Public.cshtml",
                engine.RenderFile(ctx.Template("views/catalogues_public.sbn"), new { primary }), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Catalogues/PublicDetails.cshtml",
                engine.RenderFile(ctx.Template("views/catalogues_public_details.sbn"), new { primary }), ct);
            // Task 19: rename own catalogue.
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Catalogues/EditName.cshtml",
                engine.RenderFile(ctx.Template("views/catalogues_edit_name.sbn"), new { primary }), ct);
        }

        // A9 / Task 26 / Task 7: shared user-aggregation stack (view model + service + partial) reused by
        // the public profile (Client/Profiles/Public) AND the admin user detail (Admin/Users/Details).
        // Gated on posts||catalogues (same as the profile surface) - Program.cs registers the service
        // under the identical condition (kept in sync; drift = startup DI crash, see changelog 033).
        if (FeatureEnabled(spec, "posts") || FeatureEnabled(spec, "catalogues"))
        {
            await EmitModelsStage.WriteAsync(ctx, "Models/UserProfileViewModel.cs",
                engine.RenderFile(ctx.Template("models/user_profile_view_model.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Services/UserProfileService.cs",
                engine.RenderFile(ctx.Template("controllers/user_profile_service.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Views/Shared/_UserProfileContent.cshtml",
                engine.RenderFile(ctx.Template("views/user_profile_content_partial.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Controllers/ProfilesController.cs",
                engine.RenderFile(ctx.Template("controllers/profiles_controller.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Areas/Client/Views/Profiles/Public.cshtml",
                engine.RenderFile(ctx.Template("views/profiles_public.sbn")), ct);
        }

        // Views: layout replaced wholesale, then each declared page by kind.
        if (spec.Views is { } views)
        {
            await EmitModelsStage.WriteAsync(ctx, views.Layout.File,
                engine.RenderFile(ctx.Template("views/layout.sbn"),
                    new { design_overlay_html = RenderDesignOverlay(ctx, engine) }), ct);
            foreach (var page in views.Pages)
            {
                var template = page.Kind switch
                {
                    "landing" => "views/landing.sbn",
                    "contactFormStatic" => "views/contact.sbn",
                    "cardGrid" => "views/card_grid.sbn",
                    "adminTable" => "views/admin_table.sbn",
                    _ => throw new InvalidOperationException($"Unknown page kind '{page.Kind}' for {page.File}"),
                };
                var model = ModelForPage(spec, page);
                var owner = ControllerForPage(spec, page); // lets templates respect removedActions/sorting
                await EmitModelsStage.WriteAsync(ctx, page.File,
                    engine.RenderFile(ctx.Template(template), new { page, model, owner }), ct);
            }
        }

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("build after emit-controllers-views");
        ctx.Info("controllers+views emitted, build green");
    }

    /// <summary>Renders the A10 design overlay stylesheet to inline into the layout head.</summary>
    internal static string RenderDesignOverlay(StageContext ctx, TemplateEngine engine)
    {
        var path = ctx.Template("views/design_overlay.sbn");
        return File.Exists(path) ? engine.RenderFile(path) : "";
    }

    internal static bool FeatureEnabled(ProjectSpec spec, string name) =>
        spec.Features.TryGetValue(name, out var cfg)
        && cfg.TryGetProperty("enabled", out var en) && en.GetBoolean();

    internal static string CataloguesPrimaryModel(ProjectSpec spec) => FeaturePrimaryModel(spec, "catalogues");

    internal static string FeaturePrimaryModel(ProjectSpec spec, string feature) =>
        spec.Features.TryGetValue(feature, out var cfg)
            ? SpecExpandStage.ResolvePrimaryModel(spec, cfg)
            : spec.Models.First(x => x.Images is not null).Name;

    internal static ControllerSpec? ControllerForPage(ProjectSpec spec, PageSpec page)
    {
        var parts = page.File.Split('/');
        var viewsIdx = Array.IndexOf(parts, "Views");
        if (viewsIdx < 0 || viewsIdx + 1 >= parts.Length) return null;
        return spec.Controllers.FirstOrDefault(c => c.Name == parts[viewsIdx + 1] + "Controller");
    }

    /// <summary>Card grid / admin table pages infer their model from the owning controller's route segment.</summary>
    internal static ModelSpec? ModelForPage(ProjectSpec spec, PageSpec page)
    {
        // e.g. Areas/Admin/Views/Cars/Index.cshtml -> controller "CarsController" -> model Car
        var parts = page.File.Split('/');
        var viewsIdx = Array.IndexOf(parts, "Views");
        if (viewsIdx < 0 || viewsIdx + 1 >= parts.Length) return null;
        var controllerName = parts[viewsIdx + 1] + "Controller";
        var controller = spec.Controllers.FirstOrDefault(c => c.Name == controllerName);
        return controller?.Model is { } m ? spec.Models.FirstOrDefault(x => x.Name == m) : null;
    }

    private static async Task ScaffoldControllerAsync(StageContext ctx, ControllerSpec c, CancellationToken ct)
    {
        var spec = ctx.Spec;
        var outDir = Path.GetDirectoryName(c.File)!.Replace('\\', '/');
        var result = await Proc.RunAsync("dotnet",
            $"aspnet-codegenerator controller -name {c.Name} -m {spec.Meta.RootNamespace}.Models.{c.Model} " +
            $"-dc {spec.Meta.RootNamespace}.{spec.Meta.ProjectName}Context --relativeFolderPath {outDir} --useDefaultLayout --referenceScriptLibraries",
            ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5));
        result.ThrowIfFailed($"scaffold controller {c.Name}");

        // The codegenerator has NO -area flag (verified on SDK 10.0.100) and always drops
        // CRUD views under root Views/{Controller}/, ignoring --relativeFolderPath. For an
        // area-scoped controller they must live under Areas/{Area}/Views/{Controller}/.
        if (c.Area is { } area)
        {
            var shortName = c.Name.Replace("Controller", "");
            var scaffoldedViewsDir = Path.Combine(ctx.ProjectDir, "Views", shortName);
            var targetViewsDir = Path.Combine(ctx.ProjectDir, "Areas", area, "Views", shortName);
            if (Directory.Exists(scaffoldedViewsDir))
            {
                Directory.CreateDirectory(targetViewsDir);
                foreach (var file in Directory.GetFiles(scaffoldedViewsDir))
                {
                    File.Copy(file, Path.Combine(targetViewsDir, Path.GetFileName(file)), overwrite: true);
                    File.Delete(file);
                }
                Directory.Delete(scaffoldedViewsDir);
            }
        }
    }

    /// <summary>Scaffolder never adds [Area(...)] or the area namespace - inject both (verified on SDK 10.0.100).</summary>
    internal static void InjectAreaAttribute(StageContext ctx, ControllerSpec c, string area)
    {
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);
        if (source.Contains("[Area(")) return; // idempotent rerun

        source = source.Replace($"namespace {ctx.Spec.Meta.RootNamespace}.Controllers",
                                 $"namespace {ctx.Spec.Meta.RootNamespace}.Areas.{area}.Controllers");
        if (!source.Contains("using Microsoft.AspNetCore.Mvc;"))
            source = "using Microsoft.AspNetCore.Mvc;\n" + source;

        var rx = new Regex($@"public\s+class\s+{c.Name}\b");
        var m = rx.Match(source);
        if (!m.Success)
            throw new InvalidOperationException($"InjectAreaAttribute: class declaration not found in {c.File}.");
        source = source.Insert(m.Index, $"[Area(\"{area}\")]\n");
        File.WriteAllText(path, source);
        ctx.Info($"injected [Area(\"{area}\")] into {c.Name}");
    }

    /// <summary>
    /// B2: rewrite scaffolder EN residue ("Back to List", "Edit", h1 headers, submit values)
    /// to Bulgarian using templates/bg-term-map.json, and restyle bare action links as
    /// buttons. Runs over every view of the scaffolded controller (all areas). Idempotent:
    /// replacements are exact-match, absent after first pass.
    /// </summary>
    internal static void ApplyBgResiduePass(StageContext ctx, ControllerSpec c)
    {
        var mapPath = Path.Combine(ctx.GenesisRoot, "templates", "bg-term-map.json");
        if (!File.Exists(mapPath)) return;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(mapPath));
        var exact = doc.RootElement.GetProperty("exact").EnumerateObject()
            .Select(p => (p.Name, p.Value.GetString()!)).ToList();
        var buttonize = doc.RootElement.TryGetProperty("buttonize", out var b)
            ? b.EnumerateArray().Select(e => (e.GetProperty("match").GetString()!, e.GetProperty("class").GetString()!)).ToList()
            : [];

        foreach (var file in ViewFilesOf(ctx, c))
        {
            var src = File.ReadAllText(file);
            var before = src;
            foreach (var (from, to) in exact) src = src.Replace(from, to);
            foreach (var (match, cls) in buttonize)
                // only add a class where the anchor has none - leave already-styled links alone
                src = src.Replace(match + " asp-route", match + $" class=\"{cls}\" asp-route")
                         .Replace(match + ">", match + $" class=\"{cls}\">");
            if (src != before) { File.WriteAllText(file, src); ctx.Info($"BG residue pass: {Path.GetFileName(file)}"); }
        }
    }

    /// <summary>
    /// A2: system-owned fields (CreatedAt/UpdatedAt/DeletedAt/IsDeleted) must never be
    /// user-editable. Removes their form-groups from Create/Edit, their [Bind] entries in
    /// the controller, and their display rows from CLIENT-area Details/Delete (admin keeps
    /// read-only display). SaveChanges override owns them entirely.
    /// </summary>
    internal static void StripSystemFieldsFromViews(StageContext ctx, ControllerSpec c)
    {
        string[] fields = ["CreatedAt", "UpdatedAt", "DeletedAt", "IsDeleted"];

        // controller [Bind("A,B,CreatedAt,...")] lists: rewrite each list without the
        // system entries (parse -> filter -> rejoin; never blind string surgery).
        var ctrlPath = Path.Combine(ctx.ProjectDir, c.File);
        if (File.Exists(ctrlPath))
        {
            var ctrlSrc = File.ReadAllText(ctrlPath);
            var beforeCtrl = ctrlSrc;
            ctrlSrc = System.Text.RegularExpressions.Regex.Replace(ctrlSrc,
                @"\[Bind\(""([^""]*)""\)\]",
                m =>
                {
                    var kept = m.Groups[1].Value.Split(',')
                        .Select(x => x.Trim())
                        .Where(x => !fields.Contains(x));
                    return $"[Bind(\"{string.Join(",", kept)}\")]";
                });
            if (ctrlSrc != beforeCtrl) { File.WriteAllText(ctrlPath, ctrlSrc); ctx.Info($"system fields removed from [Bind] in {c.Name}"); }
        }

        var isClient = string.Equals(c.Area, "Client", StringComparison.OrdinalIgnoreCase);
        foreach (var file in ViewFilesOf(ctx, c))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var isForm = name is "Create" or "Edit";
            var isDisplay = name is "Details" or "Delete" or "Index";
            if (!isForm && !(isClient && isDisplay)) continue; // admin keeps read-only display

            var src = File.ReadAllText(file);
            var before = src;
            foreach (var f in fields)
            {
                // form group: <div class="form-group"> ... asp-for="F" ... </div>
                src = System.Text.RegularExpressions.Regex.Replace(src,
                    $@"<div class=""form-group"">\s*(?:(?!</div>).)*?\b{f}\b(?:(?!</div>).)*?</div>\s*",
                    "", System.Text.RegularExpressions.RegexOptions.Singleline);
                // display pair: <dt ...>...F...</dt> <dd ...>...F...</dd>
                src = System.Text.RegularExpressions.Regex.Replace(src,
                    $@"<dt[^>]*>\s*(?:(?!</dt>).)*?\b{f}\b(?:(?!</dt>).)*?</dt>\s*<dd[^>]*>(?:(?!</dd>).)*?</dd>\s*",
                    "", System.Text.RegularExpressions.RegexOptions.Singleline);
                // table column in Index: header + per-row cells are positional - skip tables (low risk, admin only)
            }
            if (src != before) { File.WriteAllText(file, src); ctx.Info($"system fields stripped: {Path.GetFileName(file)} ({c.Name})"); }
        }
    }

    /// <summary>
    /// A3/A14: the rich product Details view expects ViewData["Reviews"], ["AverageRating"],
    /// ["ReviewAuthors"], ["CurrentUserId"]. Inject that load into the scaffolded Details
    /// action of the rating feature's primary-model CLIENT controller, plus the image Include.
    /// </summary>
    internal static void InjectReviewLoadingIntoDetails(StageContext ctx, ControllerSpec c)
    {
        if (!FeatureEnabled(ctx.Spec, "rating")) return;
        if (!string.Equals(c.Area, "Client", StringComparison.OrdinalIgnoreCase)) return;
        if (c.Model is null || c.Model != FeaturePrimaryModel(ctx.Spec, "rating")) return;

        var path = Path.Combine(ctx.ProjectDir, c.File);
        if (!File.Exists(path)) return;
        var src = File.ReadAllText(path);
        if (src.Contains("ViewData[\"Reviews\"]")) return; // idempotent

        // Ensure the controller has UserManager injected; scaffolded ones don't.
        if (!src.Contains("UserManager<ApplicationUser>"))
        {
            src = src.Replace($"using {ctx.Spec.Meta.RootNamespace}.Models;",
                $"using {ctx.Spec.Meta.RootNamespace}.Models;\nusing Microsoft.AspNetCore.Identity;");
            // add field + ctor param - scaffolded ctor takes only the context
            var ctorRx = new Regex($@"public {c.Name}\(([^)]*){ctx.Spec.Meta.ProjectName}Context (\w+)\)");
            var m = ctorRx.Match(src);
            if (m.Success)
            {
                var ctxParam = m.Groups[2].Value;
                src = src.Replace(m.Value,
                    $"private readonly UserManager<ApplicationUser> _userManager;\n" +
                    $"    public {c.Name}({m.Groups[1].Value}{ctx.Spec.Meta.ProjectName}Context {ctxParam}, UserManager<ApplicationUser> userManager)");
                // assign in body
                var bodyRx = new Regex($@"(public {c.Name}\([^)]*\)\s*\{{)");
                src = bodyRx.Replace(src, "$1\n        _userManager = userManager;", 1);
            }
        }

        // Replace the Details action body's data load. Scaffolded shape:
        //   var x = await _context.Products.FirstOrDefaultAsync(m => m.Id == id);
        var loadRx = new Regex(
            @"var (\w+) = await _context\." + c.Model + @"s(\s*\.Include\([^;]*?)?\s*\.FirstOrDefaultAsync\(([^;]+?)\);",
            RegexOptions.Singleline);
        var lm = loadRx.Match(src);
        if (lm.Success)
        {
            var varName = lm.Groups[1].Value;
            var pred = lm.Groups[3].Value;
            var replacement =
                $"var {varName} = await _context.{c.Model}s\n" +
                $"            .Include(x => x.Images)\n" +
                $"            .Include(x => x.Reviews)\n" +
                $"            .FirstOrDefaultAsync({pred});\n" +
                $"        if ({varName} is not null)\n" +
                $"        {{\n" +
                $"            var reviews = {varName}.Reviews.Where(r => !r.IsDeleted).OrderByDescending(r => r.CreatedAt).ToList();\n" +
                $"            ViewData[\"Reviews\"] = reviews;\n" +
                $"            ViewData[\"AverageRating\"] = reviews.Count > 0 ? reviews.Average(r => r.Stars) : (double?)null;\n" +
                $"            ViewData[\"CurrentUserId\"] = _userManager.GetUserId(User);\n" +
                $"            var authorIds = reviews.Select(r => r.AuthorUserId).Distinct().ToList();\n" +
                $"            ViewData[\"ReviewAuthors\"] = await _userManager.Users\n" +
                $"                .Where(u => authorIds.Contains(u.Id))\n" +
                $"                .ToDictionaryAsync(u => u.Id, u => u.FirstName ?? u.Email ?? \"Потребител\");\n" +
                $"        }}";
            src = loadRx.Replace(src, replacement, 1);
            File.WriteAllText(path, src);
            ctx.Info($"injected review loading into {c.Name}.Details");
        }
    }

    private static IEnumerable<string> ViewFilesOf(StageContext ctx, ControllerSpec c)
    {
        var shortName = c.Name.Replace("Controller", "");
        var dir = c.Area is { } a
            ? Path.Combine(ctx.ProjectDir, "Areas", a, "Views", shortName)
            : Path.Combine(ctx.ProjectDir, "Views", shortName);
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*.cshtml") : [];
    }

    /// <summary>
    /// conventions.deleteBehavior=soft (or model.softDelete): rewrite the scaffolded
    /// DeleteConfirmed body from physical Remove to flag-set. The global query filter
    /// in the DbContext hides flagged rows everywhere else automatically.
    /// </summary>
    internal static void ApplySoftDelete(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        var soft = (model?.SoftDelete ?? false) || ctx.Spec.Conventions.DeleteBehavior == "soft";
        if (!soft) return;
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);
        if (source.Contains("IsDeleted = true")) return; // idempotent

        var before = source;
        source = Regex.Replace(source,
            $@"_context\.{c.Model}s\.Remove\((\w+)\);",
            "$1.IsDeleted = true; $1.DeletedAt = DateTime.UtcNow; // меко изтриване (глобалният филтър го скрива)");
        if (source != before)
        {
            File.WriteAllText(path, source);
            ctx.Info($"soft-delete transform applied to {c.Name}");
        }
    }

    /// <summary>
    /// Scaffolded Index() queries have no .Include() - cards/tables would show empty images.
    /// Injects .Include(x => x.Images) into the Index query when the model has images.
    /// </summary>
    internal static void InjectImageInclude(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        if (model?.Images is null) return;
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);
        if (source.Contains(".Include(x => x.Images)")) return; // idempotent

        var before = source;
        source = Regex.Replace(source,
            $@"_context\.{c.Model}s(\s*\.To(List|Array)Async\(\))",
            $"_context.{c.Model}s.Include(x => x.Images)$1");
        if (source != before)
        {
            if (!source.Contains("using Microsoft.EntityFrameworkCore;"))
                source = "using Microsoft.EntityFrameworkCore;\n" + source;
            File.WriteAllText(path, source);
            ctx.Info($"injected image Include into {c.Name}.Index");
        }
    }

    /// <summary>
    /// Declarative deviations over scaffolded output. Each transform throws if its
    /// anchor pattern is missing - loud beats wrong.
    /// </summary>
    internal static void ApplyDeviations(StageContext ctx, ControllerSpec c, TemplateEngine engine)
    {
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);

        foreach (var removed in c.RemovedActions)
        {
            // Method start (tolerant: optional GET/POST comment, attributes, CRLF, async or not),
            // then brace-walk to the matching close - immune to nested braces and formatting.
            var startRx = new Regex(
                $@"([ \t]*//\s*(GET|POST):[^\r\n]*\r?\n)?(\s*\[[^\]]+\]\r?\n)*\s*public\s+(async\s+)?Task<IActionResult>\s+{removed}\s*\(",
                RegexOptions.Singleline);
            var mm = startRx.Match(source);
            if (!mm.Success)
            {
                // Idempotent rerun: already removed by a prior (partially failed) run.
                ctx.Info($"deviation removedActions:{removed} - not present in {c.File} (already applied?), skipping");
                continue;
            }
            var braceStart = source.IndexOf('{', mm.Index + mm.Length);
            var depth = 0; var endIdx = braceStart;
            for (var i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) { endIdx = i; break; }
            }
            if (depth != 0)
                throw new InvalidOperationException($"Deviation removedActions:{removed} - unbalanced braces in {c.File}.");
            source = source.Remove(mm.Index, endIdx - mm.Index + 1);

            var viewPath = Path.Combine(ctx.ProjectDir, Path.GetDirectoryName(c.File)!.Replace("Controllers", "Views"),
                c.Name.Replace("Controller", ""), removed + ".cshtml");
            if (File.Exists(viewPath)) File.Delete(viewPath);
        }

        foreach (var red in c.Redirects)
        {
            var (targetController, targetAction) = (red.To.Split('.')[0].Replace("Controller", ""), red.To.Split('.')[1]);
            var rx = new Regex(
                $@"(public\s+(async\s+)?Task<IActionResult>\s+{red.After}\b(?:(?!public\s+(async\s+)?Task).)*?)return\s+RedirectToAction\(nameof\(Index\)\);",
                RegexOptions.Singleline);
            var before = source;
            source = rx.Replace(source,
                $"$1return RedirectToAction(\"{targetAction}\", \"{targetController}\");", 1);
            if (source == before)
            {
                if (source.Contains($"RedirectToAction(\"{targetAction}\", \"{targetController}\")"))
                { ctx.Info($"deviation redirect after {red.After} - already applied, skipping"); continue; }
                throw new InvalidOperationException($"Deviation redirect after {red.After} - anchor not found in {c.File}.");
            }
        }

        if (c.Sorting is { Enabled: true, ImplementedIn: "controller" } sSort)
        {
            // Idempotency marker: ViewData["TotalPages"] only appears in the sorting+pagination
            // Index body we emit - independent of the exact parameter list, which has changed
            // shape before (adding search/page) and may again.
            if (source.Contains("ViewData[\"TotalPages\"]"))
            {
                ctx.Info("deviation sorting - already applied, skipping");
            }
            else
            {
                var snippet = engine.RenderFile(ctx.Template("controllers/sorting_index_body.sbn"), new { controller = c });
                // Anchor on ANY Index(...) action - scaffolded (no params) or a previous
                // differently-shaped generated one - not just the parameterless scaffolded shape.
                var rx = new Regex(
                    @"public\s+(async\s+)?Task<IActionResult>\s+Index\([^)]*\)\s*\{",
                    RegexOptions.Singleline);
                var m = rx.Match(source);
                if (!m.Success)
                    throw new InvalidOperationException($"Deviation sorting - Index() method not found in {c.File}.");
                var braceStart = source.IndexOf('{', m.Index + m.Length - 1);
                var depth = 0; var endIdx = braceStart;
                for (var i = braceStart; i < source.Length; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}' && --depth == 0) { endIdx = i; break; }
                }
                if (depth != 0)
                    throw new InvalidOperationException($"Deviation sorting - unbalanced braces in {c.File}.");
                source = source.Remove(m.Index, endIdx - m.Index + 1).Insert(m.Index, snippet.TrimEnd());
            }
        }

        File.WriteAllText(path, source);
        ctx.Info($"deviations applied to {c.Name}: -{c.RemovedActions.Count} actions, {c.Redirects.Count} redirects, sorting={(c.Sorting?.Enabled ?? false)}");
    }
}
