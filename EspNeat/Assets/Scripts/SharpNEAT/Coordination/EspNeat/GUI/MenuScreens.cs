using UnityEngine;

namespace SharpNeat.Coordination
{
    /// <summary>
    /// We will use this enumerator to determine which screen to show.
    /// We have Play screen, used when an evolutionary process is active, 
    /// AddModule screen, used to determine the initial conditions for new modules,
    /// and MainMenu, where the user may decide whether to start a new evolutionary
    /// process or edit the module structure.
    /// </summary>
    public enum MenuScreens
    {
        Play,
		Edit,
		// These are all to be upgraded or deleted.
        PlayEditWeights,
        EditModules,
		EditInToReg,
        EditInToRegGetInfo,
        AddModule,
        AddModuleInit,
        AddModuleLocalIn,
        AddModuleLocalOut,
        AddModuleRegulation,
        AddModuleLabels,
        ProtectedWeights,
        MainMenu,
        MainMenuReset,
        MainMenuResetActive
    }
}