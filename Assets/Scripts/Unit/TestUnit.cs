namespace MiniChess
{
    // Concrete unit for prototype tests. Uses Unit's shared behavior without skills.
    public sealed class TestUnit : Unit
    {
        protected override UnitDefinition DefaultDefinition => UnitDefinitions.Test;
    }
}

