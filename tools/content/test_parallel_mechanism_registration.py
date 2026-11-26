"""Merge feature-only branches, then build, execute and serialize both mechanisms."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ParallelMechanismRegistrationTests(unittest.TestCase):
    # The two registration tests each build Core twice in a scratch repo (~100 s apiece); CI's "py" part sets this and its
    # "registration" part runs them side by side (issue #798). The full local/Mac run leaves it unset and still runs them.
    @unittest.skipIf(os.environ.get("L5R_SKIP_REGISTRATION_BUILD"), "runs in the registration CI part")
    def test_two_feature_files_merge_and_both_execute(self):
        with tempfile.TemporaryDirectory(prefix="l5r-mechanisms-") as directory:
            root = Path(directory)
            for folder in ("src/L5R.Core", "content/published"):
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
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AssemblyName>L5R.Core.Tests</AssemblyName></PropertyGroup>
<ItemGroup><ProjectReference Include="../src/L5R.Core/L5R.Core.csproj" /></ItemGroup></Project>''', encoding="utf-8")
            (probe / "Program.cs").write_text('''using L5R.Core;
using System.Text.Json;
var state = MatchEngine.CreatePublishedTestMatch().State;
state.Phase = Phase.Action; state.ActivePlayerId = 0; state.PriorityPlayerId = 0;
var engine = MatchEngine.FromTestState(state);
foreach (var id in AbilityCatalog.All.Keys.Where(id => id.StartsWith("parallel-", StringComparison.Ordinal)))
{
    var result = engine.Execute(new GameCommand("useAbility", engine.State.PriorityPlayerId, engine.Version, AbilityId: id));
    if (!result.Accepted) throw new Exception(result.Error);
}
var json = JsonSerializer.Serialize(engine.State);
var restored = JsonSerializer.Deserialize<MatchState>(json)!;
if (JsonSerializer.Serialize(restored) != json) throw new Exception("Feature state did not round trip");
Console.WriteLine(JsonSerializer.Serialize(new { Cards = CardCatalog.Cards.Values, Trace = engine.State.TokenSequence,
    Hits = JsonDocument.Parse(json).RootElement.EnumerateObject().Where(p => p.Name.StartsWith("ParallelHits", StringComparison.Ordinal)).ToDictionary(p => p.Name, p => p.Value.GetInt32()) }));
''', encoding="utf-8")
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
                return json.loads(run("dotnet", "probe/bin/Debug/net10.0/L5R.Core.Tests.dll"))

            run("git", "init", "-b", "base")
            run("git", "add", ".")
            run("git", "commit", "-m", "Shared registration baseline")
            before = snapshot()
            for branch, number in (("left", 10), ("right", 2)):
                run("git", "switch", "-c", branch, "base")
                feature = f"Parallel{number}"
                text = f'''namespace L5R.Core;
public sealed partial class MatchEngine
{{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Feature registration")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register{feature}()
    {{
        EngineHooks.Abilities.Register("{feature}", 10000 + {number}, static all =>
        {{ all.Add("parallel-{number}", new("Open", "Parallel {number}", "none", PlayerAbility: true)); return all; }});
        EngineHooks.RunEffects.Register("{feature}", 10000 + {number}, static (engine, context) =>
        {{
            if (context.id != "parallel-{number}") return null;
            engine._state.ParallelHits{number} += {number};
            engine._state.TokenSequence = engine._state.TokenSequence * 100 + {number};
            return false;
        }});
    }}
}}
public sealed partial class MatchState
{{
    [System.Text.Json.Serialization.JsonPropertyOrder(10000 + {number})]
    public int ParallelHits{number} {{ get; set; }}
}}
'''
                (root / f"src/L5R.Core/{feature}.cs").write_text(text, encoding="utf-8")
                run("git", "add", ".")
                run("git", "commit", "-m", f"Register {feature} in its own file")
                self.assertEqual(run("git", "diff", "--name-only", "base...HEAD").splitlines(), [f"src/L5R.Core/{feature}.cs"])
            run("git", "switch", "left")
            run("git", "merge", "--no-edit", "right")
            self.assertEqual(run("git", "ls-files", "--unmerged"), "")
            after = snapshot()
            self.assertEqual(after["Cards"], before["Cards"])
            self.assertEqual(before["Hits"], {})
            self.assertEqual(after["Hits"], {"ParallelHits2": 2, "ParallelHits10": 10})
            self.assertEqual(after["Trace"], 210)



if __name__ == "__main__":
    unittest.main()
