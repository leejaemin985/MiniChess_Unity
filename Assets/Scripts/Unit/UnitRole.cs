namespace MiniChess
{
    public enum UnitRole
    {
        Combat,
        Guardian,
        Controller
    }

    // Draft slots. A roster takes exactly one unit from each position.
    public enum UnitPosition
    {
        Melee,
        Guard,
        Ranged,
        Control
    }

    // Always-on effects. Aim stays a number on the definition, not a passive.
    public enum UnitPassive
    {
        None,
        IsolationHunt, // +1 damage when the target has no allies of its own beside it.
        Ember,         // +1 damage against an enemy standing on this unit's own fire.
        SpatialEcho,   // Allies this unit repositions shrug off 1 of their next hit.
        Impact,        // +1 damage after this unit covered 2+ tiles in one move.
        MarkedPrey     // +1 damage against an enemy this unit's trap caught.
    }
}
