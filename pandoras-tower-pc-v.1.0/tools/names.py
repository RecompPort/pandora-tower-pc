"""Build build/names.tsv, the names the recompiler gives a stripped DOL.

Three sources, most trusted first:

  tools/names-manual.tsv   names given by hand, each with its evidence
  debug strings            a function that loads the string "Name()" (or
                           "Name(): ...") names itself: the SDK's WPAD, WUD,
                           DVD and OS code prints its own name
  signatures               build/sig_guess.tsv from tools/sigmatch.py

A name that lands on two addresses, or an address with two names from the
same source, is ambiguous and dropped. A signature name is dropped where a
string or a hand name says otherwise: look-alike functions (WPAD's three
callback setters) share a signature but not a string.

    python tools/names.py build/extract/sys/main.dol
"""
import bisect
import collections
import os
import re
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from wiikit import ppc
from wiikit.dol import Image
from wiikit.recomp.discover import symbolise

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAME_RE = re.compile(r"^\s*(_*[A-Z][A-Za-z0-9_]*)\s*\(\s*\)?")


def unique(pairs):
    """{addr: name} keeping only one-to-one pairs."""
    by_name, by_addr = collections.defaultdict(set), collections.defaultdict(set)
    for a, n in pairs:
        by_name[n].add(a)
        by_addr[a].add(n)
    return {a: next(iter(ns)) for a, ns in by_addr.items()
            if len(ns) == 1 and len(by_name[next(iter(ns))]) == 1}


def string_names(img, starts):
    strs = {}
    for s in img.segments:
        if not s.text:
            for m in re.finditer(rb"[\x20-\x7e\n\t]{3,}\x00", s.data):
                strs[s.vaddr + m.start()] = m.group()[:-1].decode()
    starts_set = set(starts)
    pairs = []
    for seg in img.text_segments():
        regs = {}
        for k in range(0, len(seg.data), 4):
            a = seg.vaddr + k
            if a in starts_set:
                regs = {}
            i = ppc.decode(struct.unpack_from(">I", seg.data, k)[0])
            f = i.f
            v = None
            if i.op == "addis" and f["A"] == 0:
                regs[f["D"]] = (f["simm"] << 16) & 0xFFFFFFFF
                continue
            if i.op == "addi" and f["A"] in regs:
                v, dst = (regs[f["A"]] + f["simm"]) & 0xFFFFFFFF, f["D"]
            elif i.op == "ori" and f["S"] in regs:
                v, dst = regs[f["S"]] | f["uimm"], f["A"]
            if v is not None:
                regs[dst] = v
                m = NAME_RE.match(strs.get(v, ""))
                if m:
                    pairs.append((starts[bisect.bisect_right(starts, a) - 1], m.group(1)))
            elif "D" in f and f["D"] in regs and not i.op.startswith("st"):
                del regs[f["D"]]
    return unique(pairs)


def read_tsv(path, name_col):
    out = []
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            next(f)
            for line in f:
                p = line.rstrip("\n").split("\t")
                if len(p) > name_col and p[0].strip():
                    out.append((int(p[0], 16), p[name_col]))
    return out


def main():
    img = Image(sys.argv[1])
    units, _ = symbolise(img, log=lambda m: None)
    starts = [s for s, _ in units]
    # per version (setup.sh): the hand-given names, the build folder, where the game's code ends
    build = os.environ.get("PANDORA_BUILD", os.path.join(ROOT, "build"))
    manual = dict(read_tsv(os.environ.get("PANDORA_NAMES_MANUAL", os.path.join(ROOT, "tools", "names-manual.tsv")), 1))
    game_end = int(os.environ.get("PANDORA_GAME_END", "80541000"), 16)
    strings = string_names(img, starts)
    # signature names inside the game's own code (0x800091E0..0x80541000) are look-alike
    # matches of small functions (JSystem, BTE names on game code): dropped
    sig = unique((a, n.split("(")[0].replace(" ", "_").replace("::", "__"))
                 for a, n in read_tsv(os.path.join(build, "sig_guess.tsv"), 2)
                 if not (0x800091E0 <= a < game_end))
    names, source = {}, {}
    taken = set()
    for src, table in (("manual", manual), ("string", strings), ("signature", sig)):
        for a, n in table.items():
            if a in names or n in taken:
                continue
            names[a], source[a] = n, src
            taken.add(n)
    out = os.path.join(build, "names.tsv")
    with open(out, "w", encoding="utf-8") as f:
        f.write("addr\tname\tsource\n")
        for a in sorted(names):
            f.write(f"{a:08X}\t{names[a]}\t{source[a]}\n")
    count = collections.Counter(source.values())
    print(f"{len(names)} names -> {out}: {dict(count)}")


if __name__ == "__main__":
    main()
