"""Cross-references in a stripped DOL: r13/r2 small-data accesses, lis+addi/ori
pairs (per function, register-tracked), and bl/b targets. Builds an index once
and pickles it to build/xref.pkl."""
import struct, bisect, pickle, os, sys, collections
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
R13, R2 = 0x807F79C0, 0x807FAF40
DOL = f"{ROOT}/build/extract/sys/main.dol"

def load():
    d = open(DOL, "rb").read()
    u = lambda o: struct.unpack_from(">I", d, o)[0]
    text = [(u(i*4), u(0x48+i*4), u(0x90+i*4)) for i in range(7) if u(0x90+i*4)]
    data = [(u(0x1C+i*4), u(0x64+i*4), u(0xAC+i*4)) for i in range(11) if u(0xAC+i*4)]
    return d, text, data

def starts():
    return sorted(int(l.split("\t")[0], 16) for l in open(f"{ROOT}/build/recomp/symbols.tsv").read().splitlines()[1:])

def build():
    d, text, data = load()
    st = starts(); sset = set(st)
    refs = collections.defaultdict(list)      # target -> [(insn_addr, kind)]
    calls = collections.defaultdict(list)     # callee -> [caller insn addr]
    for fo, va, sz in text:
        regs = {}
        for i in range(0, sz, 4):
            a = va + i; w = struct.unpack_from(">I", d, fo + i)[0]
            if a in sset: regs = {}
            op = w >> 26; D = (w >> 21) & 31; A = (w >> 16) & 31
            simm = w & 0xFFFF; simm -= 0x10000 if simm & 0x8000 else 0
            if op == 18:                                   # b / bl
                li = w & 0x03FFFFFC
                if li & 0x02000000: li -= 0x04000000
                t = (li if w & 2 else a + li) & 0xFFFFFFFF
                (calls if w & 1 else refs)[t].append((a, "bl" if w & 1 else "b") if not (w & 1) else a)
                continue
            if op == 15 and A == 0:                         # lis
                regs[D] = (simm << 16) & 0xFFFFFFFF; continue
            if A in (13, 2) and op in (14, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 48, 49, 50, 51, 52, 53, 54, 55):
                refs[((R13 if A == 13 else R2) + simm) & 0xFFFFFFFF].append((a, "sda"))
            if op == 14 and A in regs:                      # addi from lis
                v = (regs[A] + simm) & 0xFFFFFFFF; refs[v].append((a, "lis+addi")); regs[D] = v; continue
            if op == 24 and ((w >> 21) & 31) in regs:       # ori rA, rS
                S = (w >> 21) & 31; v = regs[S] | (w & 0xFFFF); refs[v].append((a, "lis+ori")); regs[A] = v; continue
            if op in (32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 48, 49, 50, 51, 52, 53, 54, 55) and A in regs:
                refs[(regs[A] + simm) & 0xFFFFFFFF].append((a, "lis+mem"))
            if op not in (36, 37, 38, 39, 44, 45, 52, 53, 54, 55) and D in regs:   # D overwritten (not a store)
                regs.pop(D, None)
    pickle.dump((dict(refs), dict(calls)), open(f"{ROOT}/build/xref.pkl", "wb"))
    return refs, calls

def get():
    p = f"{ROOT}/build/xref.pkl"
    return pickle.load(open(p, "rb")) if os.path.exists(p) else build()

def fn_of(a, st=None):
    st = st or starts(); return st[bisect.bisect_right(st, a) - 1]

if __name__ == "__main__":
    build(); print("xref index built")
