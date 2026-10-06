public static class DifficultyService
{
    public static DifficultySettings GetSettings(
        NetworkGameManager manager)
    {
        if (manager == null)
            return DifficultyDatabase.Get(GameDifficulty.Normal);

        return DifficultyDatabase.Get(
            manager.Difficulty
        );
    }
}