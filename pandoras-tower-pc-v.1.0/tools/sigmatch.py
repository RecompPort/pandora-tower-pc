"""Name a stripped DOL's library functions by signature.

A first pass, written for session 1's feasibility question: how much of a
stripped Dragon Quest Swords main.dol can be named without symbols? It is
game-agnostic and meant to move into wiikit (as `wiikit.sig`) once its
function discovery is replaced by the recompiler's own.

Function discovery here is crude: every `bl` target, and every non-zero word
after a `blr`. Each candidate is hashed with Dolphin's HashSignatureDB
checksum (opcodes with register fields kept, immediates and branch targets
masked) over its whole span and over each prefix ending in a `blr`, and
looked up in:

  * Dolphin's Sys/totaldb.dsy (u32 count, then {u32 checksum, u32 size,
    char name[128]} little-endian; names carry "\\tlibrary.a object.o")
  * any symbolised ELF (--elf, repeatable), e.g. Victorious's
    Oscar_wii_final_versioned.elf

Small functions collide (getters, stubs): --min-size drops matches below it.

    python tools/sigmatch.py build/extract/sys/main.dol \\
        --dsy <Dolphin>/Sys/totaldb.dsy \\
        --elf <pc-victorious>/build/extract/files/Oscar_wii_final_versioned.elf \\
        --out build/sig_guess.tsv
"""
import argparse
import collections
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # wiikit/

from wiikit.dol import Image
from wiikit.ppc import branch_target, decode

BLR = 0x4E800020


def checksum(words):
    """Dolphin's HashSignatureDB::ComputeCodeChecksum."""
    s = 0
    for o in words:
        op = o & 0xFC000000
        a = op >> 26
        op2 = op3 = 0
        if a == 4:
            op2 = o & 0x3F
            if op2 in (0, 8, 16, 21, 22):
                op3 = o & 0x7C0
        elif a in (7, 8, 10, 11, 12, 13, 14, 15):
            op2 = o & 0x03FF0000
        elif a in (19, 31, 63):
            op2 = o & 0x7FF
        elif a == 59:
            op2 = o & 0x3F
            if op2 < 16:
                op3 = o & 0x7C0
        elif 32 <= a < 56:
            op2 = o & 0x03FF0000
        s = ((s << 17) & 0xFFFE0000) | ((s >> 15) & 0x1FFFF)
        s ^= op | op2 | op3
    return s


def load_dsy(path):
    db = open(path, "rb").read()
    n = struct.unpack_from("<I", db, 0)[0]
    sig = collections.defaultdict(list)
    for k in range(n):
        cs, size = struct.unpack_from("<II", db, 4 + 136 * k)
        raw = db[12 + 136 * k:4 + 136 * (k + 1)].split(b"\0")[0].decode("latin1")
        name, _, lib = raw.partition("\t")
        sig[(cs, size)].append((name.strip(), lib.strip()))
    return sig


def load_elf(path):
    img = Image(path)
    sig = collections.defaultdict(list)
    for f in img.functions():
        if f.size % 4:
            continue
        ws = [img.u32(f.addr + i) for i in range(0, f.size, 4)]
        sig[(checksum(ws), f.size)].append((f.name, "elf"))
    return sig


def text_words(img):
    words = {}
    for s in img.text_segments():
        for i in range(0, len(s.data) - 3, 4):
            words[s.vaddr + i] = struct.unpack_from(">I", s.data, i)[0]
    return words


def discover(words):
    starts = set()
    for a, w in words.items():
        ins = decode(w)
        if ins.op == "b" and ins.f["LK"]:
            t = branch_target(ins, a)
            if t in words:
                starts.add(t)
        if w == BLR and words.get(a + 4):
            starts.add(a + 4)
    return sorted(starts)


def spans(words, starts):
    for i, st in enumerate(starts):
        end = starts[i + 1] if i + 1 < len(starts) else st + 4
        ws = []
        for x in range(st, end, 4):
            if x not in words:
                break
            ws.append(words[x])
        while ws and ws[-1] == 0:
            ws.pop()
        yield st, ws


def match(words, starts, sigs, min_size):
    found = {}
    for st, ws in spans(words, starts):
        cuts = [len(ws)] + [j + 1 for j, w in enumerate(ws) if w == BLR]
        for n in cuts:
            if 4 * n < min_size:
                continue
            key = (checksum(ws[:n]), 4 * n)
            for src, sig in sigs:
                if key in sig:
                    found[st] = (4 * n, sig[key], src)
                    break
            if st in found:
                break
    return found


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("dol")
    ap.add_argument("--dsy")
    ap.add_argument("--elf", action="append", default=[])
    ap.add_argument("--min-size", type=int, default=0x20)
    ap.add_argument("--out")
    a = ap.parse_args()
    sigs = []
    if a.dsy:
        sigs.append(("dsy", load_dsy(a.dsy)))
    for e in a.elf:
        sigs.append((e.replace("\\", "/").split("/")[-1], load_elf(e)))
    img = Image(a.dol)
    words = text_words(img)
    starts = discover(words)
    found = match(words, starts, sigs, a.min_size)
    per_src = collections.Counter(src for _, _, src in found.values())
    libs = collections.Counter(c[0][1].split(".a")[0] or "?" for _, c, _ in found.values())
    print(f"{len(words)} words, {len(starts)} candidate functions, {len(found)} named "
          f"(>= {a.min_size} bytes): {dict(per_src)}")
    print("libraries:", ", ".join(f"{k} {v}" for k, v in libs.most_common(30)))
    if a.out:
        with open(a.out, "w") as f:
            f.write("addr\tsize\tname\tsource\talternatives\n")
            for st, (size, cands, src) in sorted(found.items()):
                alts = ";".join(n for n, _ in cands[1:4])
                f.write(f"{st:08X}\t{size:#x}\t{cands[0][0]}\t{src}\t{alts}\n")
        print("->", a.out)


if __name__ == "__main__":
    main()
