using ModLoader;

namespace WorldBuilderCoop
{
    /// <summary>
    /// Logger du mod. Les logs verbeux (réseau/réplication, par paquet/par frame) passent par
    /// Debug() et sont SILENCIEUX par défaut (prod). Mettre <see cref="Verbose"/> à true pour
    /// les réactiver en débogage. Les erreurs (Error) sont toujours affichées.
    /// </summary>
    public static class WbLog
    {
        /// <summary>Activer pour réafficher les logs verbeux de debug.</summary>
        public static bool Verbose = false;

        public static void Debug(string message)
        {
            if (Verbose) ConsoleBase.WriteLine(message);
        }

        public static void Debug(object message)
        {
            if (Verbose) ConsoleBase.WriteLine(message);
        }

        /// <summary>Log important toujours affiché (cycle de vie : connexion, hôte, etc.).</summary>
        public static void Info(string message) => ConsoleBase.WriteLine(message);

        public static void Error(string message) => ConsoleBase.WriteError(message);
    }
}
