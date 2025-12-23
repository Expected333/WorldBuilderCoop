using ModLoader;

namespace WorldBuilderCoop
{
    public class Core : ModBase
    {
        public static NetworkHandler Network { get; private set; }

        public override string ModName => "WorldBuilderCoop";
        public override string ModVersion => "1.0.0";
        public override string ModAuthor => "Zefire";

        protected override void OnInitialize()
        {
            Logger.Info("===================================");
            Logger.Info("  World Builder Coop Mod");
            Logger.Info("  Version: 1.0.0");
            Logger.Info("  Author: Zefire");
            Logger.Info("===================================");
            Logger.Info("Loading system ...");
            Network = new NetworkHandler();
            PatchAll();
        }
    }
}
