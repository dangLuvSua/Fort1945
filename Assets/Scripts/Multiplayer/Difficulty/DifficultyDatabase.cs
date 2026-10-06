public static class DifficultyDatabase
{
    public static DifficultySettings Get(GameDifficulty difficulty)
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return new DifficultySettings(
                    0.75f, // Enemy HP
                    0.50f, // Enemy damage
                    0.85f, // Enemy speed
                    0.80f, // Detection
                    1.25f, // Player stamina
                    1,     // Additional items
                    1.50f  // Puzzle time
                );

            case GameDifficulty.Difficult:
                return new DifficultySettings(
                    1.50f,
                    1.50f,
                    1.20f,
                    1.30f,
                    0.80f,
                    -1,
                    0.70f
                );

            default:
                return new DifficultySettings(
                    1.00f,
                    1.00f,
                    1.00f,
                    1.00f,
                    1.00f,
                    0,
                    1.00f
                );
        }
    }
}