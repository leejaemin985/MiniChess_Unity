using MiniChess;

internal static partial class Program
{
    private static void RunDeploymentScenarios()
    {
        Run("Player One starts off-board and cannot deploy before Start Game", () =>
        {
            var state = FixedDeployment.Create(includePlayerOne: false);
            var deploy = new DeploymentSystem(state);
            for (int i = 0; i < 4; i++)
                Check(state.GetUnit(One, i) == null && state.GetUnit(Two, i) != null);
            Check(!deploy.TryPlace(0, 0, 0) && !state.IsDeploymentReady());
            Check(deploy.TryStart() && !deploy.TryStart());
            Check(deploy.TryPlace(0, 0, 0) && state.GetUnit(One, 0) != null);
        });
        Run("Free deployment validates zone, bounds, occupancy and supports repositioning", () =>
        {
            var state = FixedDeployment.Create(includePlayerOne: false);
            var deploy = new DeploymentSystem(state);
            Check(deploy.TryStart());
            Check(!deploy.TryPlace(-1, 0, 0) && !deploy.TryPlace(4, 0, 0));
            Check(!deploy.TryPlace(0, -1, 0) && !deploy.TryPlace(0, state.BoardSize, 0));
            Check(!deploy.TryPlace(0, 0, -1) && !deploy.TryPlace(0, 0, 2) && !deploy.TryPlace(0, 0, 5));
            Check(deploy.TryPlace(0, 0, 0));
            var original = state.GetUnit(One, 0);
            Check(!deploy.TryPlace(1, 0, 0) && state.GetUnit(One, 1) == null);
            Check(deploy.TryPlace(0, 5, 1));
            Check(state.GetUnitAt(0, 0) == null && state.GetUnitAt(5, 1) == original);
            Check(deploy.TryPlace(0, 5, 1));
            Check(state.TurnNumber == 0 && state.PlayerOneCost == 6);
        });
        Run("All four placements unlock battle; battle locks deployment; restart clears placement", () =>
        {
            var state = FixedDeployment.Create(includePlayerOne: false);
            var turns = new TurnSystem(state);
            var deploy = new DeploymentSystem(state);
            Check(deploy.TryStart());
            for (int i = 0; i < 4; i++)
            {
                Check(!turns.TryStartBattle(One));
                Check(deploy.TryPlace(i, i, i % 2));
            }
            Check(state.IsDeploymentReady() && turns.TryStartBattle(One));
            Check(!deploy.TryPlace(0, 5, 0) && !deploy.TryStart());
            var fresh = FixedDeployment.Create(includePlayerOne: false);
            Check(!fresh.IsDeploymentReady() && fresh.GetUnit(One, 0) == null);
            Check(!new DeploymentSystem(fresh).Started);
        });
    }
}
