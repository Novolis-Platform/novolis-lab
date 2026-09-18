using FreightWing.Game;
using Novolis.Lab.SpaceCombat;
using Novolis.Raylib.Game;

var smoke = args.Any(a => a.Equals("--smoke", StringComparison.OrdinalIgnoreCase));
var contentDir = Path.Combine(AppContext.BaseDirectory, "Content");
// Prefer repo Content next to project when running from output
var repoContent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Content"));
if (Directory.Exists(repoContent) && File.Exists(Path.Combine(repoContent, "freightwing.novpack")))
    contentDir = repoContent;
else if (!Directory.Exists(contentDir))
    contentDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Content"));

using var pack = ContentPack.TryLoad(contentDir);
using var game = new FreightWingGame(pack, smoke);
RayGame.Run("FreightWing — Family Campaign", 1600, 900, game.Initialize, game.Update);
