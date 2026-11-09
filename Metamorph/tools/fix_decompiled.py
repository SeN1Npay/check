"""Одноразовая чистка артефактов декомпиляции (ilspycmd 8.2), чтобы проект собирался из исходников."""
import pathlib
import re
import shutil

root = pathlib.Path(__file__).resolve().parent.parent / "src"

for gen in root.glob("*/System.Text.RegularExpressions.Generated"):
    shutil.rmtree(gen)

pattern = re.compile(
    r"\[GeneratedRegex\((?P<args>.*?)\)\]\s*\n\s*\[GeneratedCode\([^\n]*\)\]\s*\n"
    r"(?P<indent>\s*)(?P<mods>[\w ]*?)static Regex (?P<name>\w+)\(\)\s*\{\s*return [^;]*\.Instance;\s*\}",
)


def repl(m: re.Match) -> str:
    args = m["args"]
    opts = "RegexOptions.Compiled" if "RegexOptions." not in args else ""
    args = f"{args}, {opts}" if opts else args.replace("RegexOptions.", "RegexOptions.Compiled | RegexOptions.", 1)
    ind, mods, name = m["indent"].lstrip("\n"), m["mods"], m["name"]
    return (f"private static readonly Regex _{name}Regex = new Regex({args});\n\n"
            f"{ind}{mods}static Regex {name}() => _{name}Regex;")


for cs in root.rglob("*.cs"):
    text = cs.read_text(encoding="utf-8-sig")
    new = pattern.sub(repl, text)
    new = re.sub(r"^\[module: RefSafetyRules\(\d+\)\]\r?\n", "", new, flags=re.M)
    if new != text:
        cs.write_text(new, encoding="utf-8")
        print("fixed", cs.relative_to(root))
