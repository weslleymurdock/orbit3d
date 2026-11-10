#!/usr/bin/env python3
import sys
import os
import json
import shutil

if len(sys.argv) < 3:
    sys.exit(0)

raw_search = sys.argv[1]
src_png = os.path.realpath(sys.argv[2])
search_roots = list(set([raw_search, os.path.realpath(raw_search)]))

print(f"fix-appicon: searching roots {search_roots} using {src_png}...")

target_dirs = []
r_dirs = []
for sroot in search_roots:
    if not os.path.exists(sroot):
        continue
    for root, dirs, _ in os.walk(sroot, followlinks=True):
        for d in dirs:
            if d == "appicon.appiconset":
                full = os.path.join(root, d)
                if full not in target_dirs:
                    target_dirs.append(full)
            elif d == "r" and os.path.basename(root) == "resizetizer":
                full_r = os.path.join(root, d)
                if full_r not in r_dirs:
                    r_dirs.append(full_r)

print(f"fix-appicon: found {len(target_dirs)} appicon sets: {target_dirs}")
print(f"fix-appicon: found {len(r_dirs)} resizetizer/r dirs: {r_dirs}")

# Populate dotnet_bot images in resizetizer/r
proj_dir = os.path.dirname(os.path.dirname(os.path.dirname(src_png)))
bot_src = os.path.join(proj_dir, "Resources", "Images", "dotnet_bot.png")
if os.path.isfile(bot_src):
    bot_names = ["dotnet_bot.png", "dotnet_bot@2x.png", "dotnet_bot@3x.png"]
    for rdir in r_dirs:
        for bname in bot_names:
            bdest = os.path.join(rdir, bname)
            if not os.path.exists(bdest):
                shutil.copyfile(bot_src, bdest)
                print(f"fix-appicon: created {bdest}")

sizes = [
    "16x16", "20x20", "24x24", "29x29", "32x32", "40x40", "48x48", "50x50",
    "57x57", "58x58", "60x60", "64x64", "72x72", "76x76", "80x80", "83.5x83.5",
    "87x87", "100x100", "114x114", "120x120", "128x128", "144x144", "152x152",
    "167x167", "180x180", "256x256", "512x512", "1024x1024",
    "16", "20", "24", "29", "32", "40", "48", "50", "57", "58", "60", "64",
    "72", "76", "80", "83.5", "87", "100", "114", "120", "128", "144", "152",
    "167", "180", "256", "512", "1024"
]
scales = ["", "@1x", "@2x", "@3x"]
suffixes = ["", "~ipad", "~iphone"]

names = set([
    "appiconItunesArtwork.png",
    "iTunesArtwork",
    "iTunesArtwork.png",
    "iTunesArtwork@2x",
    "iTunesArtwork@2x.png",
    "appicon.png",
    "appicon@2x.png",
    "appicon@3x.png",
])

for sz in sizes:
    for sc in scales:
        for suf in suffixes:
            names.add(f"appicon{sz}{sc}{suf}.png")

for target_dir in target_dirs:
    count = 0
    for fn in names:
        dest = os.path.join(target_dir, fn)
        if not os.path.exists(dest):
            shutil.copyfile(src_png, dest)
            count += 1

    contents_json = os.path.join(target_dir, "Contents.json")
    if not os.path.isfile(contents_json):
        default_contents = {
            "images": [
                {
                    "idiom": "universal",
                    "platform": "ios",
                    "size": "1024x1024",
                    "filename": "appiconItunesArtwork.png"
                },
                {
                    "idiom": "ios-marketing",
                    "size": "1024x1024",
                    "scale": "1x",
                    "filename": "appiconItunesArtwork.png"
                },
                {
                    "idiom": "mac",
                    "size": "512x512",
                    "scale": "2x",
                    "filename": "appiconItunesArtwork.png"
                },
                {
                    "idiom": "mac",
                    "size": "512x512",
                    "scale": "1x",
                    "filename": "appiconItunesArtwork.png"
                },
                {
                    "idiom": "iphone",
                    "size": "60x60",
                    "scale": "2x",
                    "filename": "appicon60x60@2x.png"
                },
                {
                    "idiom": "iphone",
                    "size": "60x60",
                    "scale": "3x",
                    "filename": "appicon60x60@3x.png"
                },
                {
                    "idiom": "ipad",
                    "size": "76x76",
                    "scale": "2x",
                    "filename": "appicon76x76@2x.png"
                },
                {
                    "idiom": "ipad",
                    "size": "83.5x83.5",
                    "scale": "2x",
                    "filename": "appicon83.5x83.5@2x.png"
                }
            ],
            "info": {
                "author": "xcode",
                "version": 1
            }
        }
        with open(contents_json, "w", encoding="utf-8") as f:
            json.dump(default_contents, f, indent=2)
        print(f"fix-appicon: generated {contents_json}")
    else:
        try:
            with open(contents_json, "r", encoding="utf-8") as f:
                data = json.load(f)
            for img in data.get("images", []):
                fn = img.get("filename")
                if fn:
                    dest = os.path.join(target_dir, fn)
                    if not os.path.exists(dest):
                        shutil.copyfile(src_png, dest)
                        count += 1
        except Exception as e:
            print(f"fix-appicon: error parsing {contents_json}: {e}")

    print(f"fix-appicon: populated {count} missing icons in {target_dir}")
