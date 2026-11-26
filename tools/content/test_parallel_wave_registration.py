"""Exercise actual Git merges, MSBuild embedding and runtime discovery in a temporary repo."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ParallelWaveRegistrationTests(unittest.TestCase):
    # The two registration tests each build Core twice in a scratch repo (~100 s apiece); CI's "py" part sets this and its
    # "registration" part runs them side by side (issue #798). The full local/Mac run leaves it unset and still runs them.
    @unittest.skipIf(os.environ.get("L5R_SKIP_REGISTRATION_BUILD"), "runs in the registration CI part")
    def test_two_branches_add_waves_without_shared_registration_edits(self):
        with tempfile.TemporaryDirectory(prefix="l5r-waves-") as directory:
            root = Path(directory)
            for folder in ("src/L5R.Core", "content/published", "tests/L5R.Core.Tests/PublishedWaves"):
                shutil.copytree(ROOT / folder, root / folder, ignore=shutil.ignore_patterns("bin", "obj"))
            shutil.copy2(ROOT / "content/deck-pool.json", root / "content/deck-pool.json")
            shutil.copy2(ROOT / "content/printed-text.json", root / "content/printed-text.json")  # embedded by L5R.Core (issue #1358)
            for name in ("global.json", "Directory.Build.props"):
                shutil.copy2(ROOT / name, root / name)
            (root / ".gitignore").write_text("**/bin/\n**/obj/\n", encoding="utf-8")
            # No NuGet dependencies, so a clean offline feed configuration suffices.
            (root / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding="utf-8")
            probe = root / "probe"
            probe.mkdir()
            (probe / "probe.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
<ItemGroup><ProjectReference Include="../src/L5R.Core/L5R.Core.csproj" /></ItemGroup></Project>''', encoding="utf-8")
            (probe / "Program.cs").write_text('''using L5R.Core;
using System.Text.Json;
Console.WriteLine(JsonSerializer.Serialize(new {
    Profiles = PublishedContent.Waves.Select(wave => wave.Profile),
    Cards = CardCatalog.Cards.Values
}));
''', encoding="utf-8")
            # A provenance sidecar must never be deserialized as a wave.
            (root / "content/published/parallel-wave-1-sources.json").write_text("not a snapshot", encoding="utf-8")
            env = os.environ.copy()
            env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
                       GIT_AUTHOR_NAME="Wave Test", GIT_AUTHOR_EMAIL="wave-test@example.invalid",
                       GIT_COMMITTER_NAME="Wave Test", GIT_COMMITTER_EMAIL="wave-test@example.invalid",
                       MSBUILDDISABLENODEREUSE="1")

            def run(*args):
                result = subprocess.run(args, cwd=root, env=env, text=True, capture_output=True, timeout=240)
                self.assertEqual(result.returncode, 0, f"{args}:\n{result.stdout}\n{result.stderr}")
                return result.stdout

            def snapshot():
                run("dotnet", "build", "probe/probe.csproj", "--disable-build-servers", "-p:UseSharedCompilation=false", "-m:1", "--nologo")
                return json.loads(run("dotnet", "probe/bin/Debug/net10.0/probe.dll"))

            run("git", "init", "-b", "base")
            run("git", "add", ".")
            run("git", "commit", "-m", "Shared registration baseline")
            before = snapshot()
            template = json.loads((root / "content/published/vanilla-wave-1.json").read_text(encoding="utf-8"))["Cards"][0]
            for branch, number in (("left", 10), ("right", 2)):
                run("git", "switch", "-c", branch, "base")
                profile = f"parallel-wave-{number}"
                card = dict(template, Id=f"parallel-card-{number}", Name=f"Parallel card {number}")
                (root / f"content/published/{profile}.json").write_text(json.dumps(dict(Profile=profile, Cards=[card], Decks=[])), encoding="utf-8")
                (root / f"tests/L5R.Core.Tests/PublishedWaves/{profile}.txt").write_text(card["Id"]+"\n", encoding="utf-8")
                run("git", "add", ".")
                run("git", "commit", "-m", f"Add {profile} only")
                paths = run("git", "diff", "--name-only", "base...HEAD").splitlines()
                self.assertEqual(set(paths), {f"content/published/{profile}.json", f"tests/L5R.Core.Tests/PublishedWaves/{profile}.txt"})
            run("git", "switch", "left")
            run("git", "merge", "--no-edit", "right")
            self.assertEqual(run("git", "ls-files", "--unmerged"), "")
            after = snapshot()
            self.assertEqual([p for p in after["Profiles"] if not p.startswith("parallel-wave-")], before["Profiles"])
            self.assertEqual([p for p in after["Profiles"] if p.startswith("parallel-wave-")], ["parallel-wave-2", "parallel-wave-10"])
            self.assertEqual([c for c in after["Cards"] if not c["Id"].startswith("parallel-card-")], before["Cards"])
            self.assertEqual({c["Id"] for c in after["Cards"] if c["Id"].startswith("parallel-card-")}, {"parallel-card-2", "parallel-card-10"})
            for number in (2, 10):
                self.assertEqual((root / f"tests/L5R.Core.Tests/PublishedWaves/parallel-wave-{number}.txt").read_text().strip(), f"parallel-card-{number}")


if __name__ == "__main__":
    unittest.main()
