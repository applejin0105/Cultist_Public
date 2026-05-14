using System.IO;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// 파일 경로 상수 모음
    /// </summary>
    public static class PathConstants
    {
        // StreamingAssets File Path
        public static string CardDbSourceFilePath =>
            Path.Combine(Application.streamingAssetsPath, "cardDB", "cardDB.json");

        public static string SampleDeckDBSourceFilePath =>
            Path.Combine(Application.streamingAssetsPath, "decks", "sampleDecks.json");

        // Persistent Folder Path
        public static string CardDbTargetFolderPath =>
            Path.Combine(Application.persistentDataPath, "cardDB");

        public static string DeckTargetFolderPath =>
            Path.Combine(Application.persistentDataPath, "decks");

        // Persistent File Path
        public static string CardDbTargetFilePath =>
            Path.Combine(CardDbTargetFolderPath, "cardDB.json");

        public static string SampleDeckDBTargetFilePath =>
            Path.Combine(DeckTargetFolderPath, "sampleDecks.json");

        public static string PlayerDeckTargetFilePath =>
            Path.Combine(DeckTargetFolderPath, "playerDecks.json");

        public static string DeckTargetFilePath =>
            Path.Combine(DeckTargetFolderPath, $"playerDecks.json");

        public static string EffectSourceFolderPath => Path.Combine(Application.streamingAssetsPath, "effects");
        public static string EffectTargetFolderPath => Path.Combine(Application.persistentDataPath, "effects");
        public static string CardEffectsTargetFilePath => Path.Combine(EffectTargetFolderPath, "cardsEffects.json");
        // 옛 commands.json / conditions.json / triggers.json / schemas 는 폐지됨.
        // 명령어·조건은 EffectsBootstrap에서 코드로 직접 등록한다.
    }
}