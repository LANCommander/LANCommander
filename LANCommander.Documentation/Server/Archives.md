---
title: Archives
---

# Archives

Games, redistributables, and tools are distributed to clients as ZIP archives. When a launcher installs a game it **streams the archive straight from the server and extracts it on the fly**. This keeps installs fast and avoids needing double the disk space, but it means the archive has to be readable from start to finish without seeking backwards.

Most ZIP files satisfy this without any special effort. A small number of archives, however, are written in a layout that a streaming reader cannot extract reliably. This page explains how to create streaming-safe archives and how to fix existing ones.

## Why some archives fail to install

A streaming reader discovers each file's boundaries as the bytes arrive. For that to work, every entry must declare its size up front, in its **local file header**.

Some archiving tools instead write entries in a *streaming* layout, where the size is unknown when the entry starts and is recorded afterwards in a trailing **data descriptor**. For compressed entries this is fine as the compressed bytes cannot be mistaken for the data descriptor. However, if you store an uncompressed entry that _happens to be another archive_, this can confuse the streaming reader and cause it to improperly determine the size of the entry being extracted.

:::info
The problematic combination is specifically **stored (uncompressed) + streaming data descriptor**. A stored entry whose size *is* in its local header installs fine at any size, and compressed entries are unaffected.
:::

## Creating streaming-safe archives

The safest rule of thumb: **create archives with a tool writing to a file** (not piping to a stream), and let large already-compressed payloads be stored with their sizes recorded normally. Writing to a real file lets the tool go back and fill in each entry's size in its local header instead of using a streaming data descriptor.

| Tool | Recommendation |
|------|----------------|
| **7-Zip** (GUI or `7z`) | Safe by defaul. Use a normal `Add to archive` / `7z a archive.zip files` and write to a _local disk_, not a network share. |
| **Windows Explorer** (Send to → Compressed folder) | Safe by default. |
| **Info-ZIP `zip`** | Safe when writing to a file (`zip -r archive.zip folder`). Avoid piping to stdout (`zip - ...`), which forces streaming data descriptors. |
| **PowerShell `Compress-Archive`** | Safe by default, do not write to a network share. |

For payloads larger than 4 GiB, make sure the tool produces a **ZIP64** archive (all the tools above do this automatically when needed).

:::info
Future versions of the launcher will have a built-in packaging tool to help in the creation of archives.
:::

## Checking and repacking existing archives

The server can detect and fix archives that use the problematic layout. On a game's **Archives** tab, each uploaded archive has a **Check Streaming Compatibility** action (the shield icon).

1. Click **Check Streaming Compatibility** on an archive.
2. If the archive is safe, you'll get a confirmation message and nothing else happens.
3. If it contains stored entries written with a streaming data descriptor, you'll be prompted to **repack** it.
4. Choosing to repack queues a background job that rewrites the archive into a streaming-safe layout. Compression is preserved per entry so the repack does not waste time re-compressing already-compressed data. The job runs in the background and can take a while for multi-gigabyte archives.

Repacking rewrites the file in place once complete and recalculates its reported sizes. File contents (and their CRCs) are unchanged; only the archive's internal layout is corrected.

:::warning
Repacking reads and rewrites the entire archive, so it temporarily needs free disk space roughly equal to the archive's size in the same storage location.
:::