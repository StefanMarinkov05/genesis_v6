# The image pipeline — the most beautiful images, never broken

Condensed from `docs/explanation/image-pipeline.md` (the design record behind ADR-011). This is a
comeback story, folks. One of the great comebacks. The images were in trouble — real trouble — and
now they're doing fantastic. Let me tell you what happened.

People used to come up to me and say, "Sir, why is that avatar broken?" Not anymore. Nobody asks
anymore. Nobody. Because the images got fixed, and they got fixed properly, believe me.

## Three defects, found in the real code

Not assumed. Found. Verified against the code:

1. **Downloads repeated on every regenerate.** `SeedStage.DownloadImageAsync` short-circuited on
   `File.Exists(targetPath)`, but the target lived inside the project directory — and since
   changelog 019 every run writes to a fresh `out/run_<timestamp>/<Project>/…` folder. So the
   "cache" sat inside disposable output, never survived a clean regenerate, and every run hit
   Pexels/Pixabay/Picsum again for the identical keywords. Same keywords! Same pictures! Paid for
   twice. Bad business.
2. **Raw external URLs were served — worst on the avatars.** The identity seeder baked a Picsum URL
   into `ProfilePicturePath`, a live external hit on every avatar render. Worse: four of the five
   avatar render sites built `"/" + ProfilePicturePath`, producing the broken path
   `/https://picsum.photos/…`. Seeded avatars were effectively broken. A total mess.
3. **The generic fallbacks did not exist.** `/images/avatar-default.png` was referenced by five
   views and never emitted — a 404 dressed up as a fallback. There was no product fallback at all;
   on a failed download the stage wrote a 1×1 transparent PNG, which shows up as a broken tile.
   A one-pixel image. One pixel! Very low energy.

## The four pillars of the fix

### 1. A persistent, cross-run cache — download once, for real

The cache lives **outside** `out/`, in the workspace: `{cwd}/.image-cache/` — the repo root in a
checkout, `Documents\DPS-NN` when installed (never the read-only install root, ADR-014). It's
gitignored, local, and persistent across runs. Files are keyed by provider + keyword, so
keyword→image is stable. Resolution order:

1. the `--image-pack` directory, if given — a local pack still wins;
2. **cache hit** → copy from the cache into the project, no network;
3. miss → download → write to the cache → copy into the project.

The per-target "already exists" check stays, for idempotency inside one run. The cache is
permanent until you clear it by hand. No max-age, no refresh flag — keyword→image is meant to be
stable, and invalidation is scope nobody needed.

### 2. Shipped fallbacks — as SVG, not binary PNG

Two text assets in the repo's `templates/static/` folder: `avatar-default.svg` (a neutral silhouette) and
`product-default.svg` (a neutral product placeholder). SVG renders in `<img>`, scales, is tiny, and
diffs like text — a generator repository shouldn't carry hand-made binaries when a text file does
the job better. A static-asset copy step in the scaffold stage places them into every generated
project's `wwwroot/images/`, idempotently.

### 3. Profile pictures become local, never URLs

The seeder stops writing the Picsum URL: seeded users get `ProfilePicturePath` left empty, and
render the shipped `avatar-default.svg`. Uploaded pictures already store a **relative** path
(`uploads/avatars/<guid>.jpg`), so once URL seeding is gone, every value is relative, and all five
render sites use one form: empty → `/images/avatar-default.svg`, else `"/" + path`. The old
`StartsWith("http")` special case goes away. Two code paths computing one concept with different
rules — that's a bug factory. We closed the factory. Distinct real faces per seeded user are
deliberately out of scope: privacy-fraught, and over-engineering.

### 4. A product fallback on a missing or failed image

A failed download no longer writes the 1×1 PNG. The target is simply absent, and the render layer
(card grid, product details, the admin table's `*Path` column) shows `/images/product-default.svg`
when the value is empty. Clean. Honest. Never a broken tile.

## What it changed in the contracts

- **No new spec fields.** `images.storedIn`, `images.mode` and the seed's `imageKeywords` rule are
  unchanged.
- **Seed data:** profile pictures leave the seed surface entirely — they were always created by the
  identity seeder, never by the seed file. Product image rows are unchanged.
- **Repo assets:** the two SVGs, and a gitignored `.image-cache/`.
- **Verify:** the copied SVGs and the cache are inert to the language gate and the manifest.

The result you can see in any generated app: no hot-linking, no repeat requests for the same
phrase, no broken images. And the lesson the design doc insists on, which I insist on too: a
render-tested-only change hides bugs. Generate the app, open a browser, look at an avatar and a
product with no image. Eyes on the page. That's how you know.
