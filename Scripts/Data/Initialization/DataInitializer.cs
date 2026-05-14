using System.IO;
using System.Threading.Tasks;
using Data.Models;
using UnityEngine;
using Utils;
using Newtonsoft.Json;

namespace Data.Initialization
{
    /// <summary>
    /// 기본 데이터 파일 복사 및 폴더 생성
    /// </summary>
    public static class DataInitializer
    {
        public static async Task InitJsonFiles()
        {
            // 정적 데이터들
            await InitCardDBData();
            await InitSampleDeckDBData();

            // 동적 데이터 (플레이어덱 세이브 데이터. 추후에 플레이어 승률과 같은 정보도 추가할 예정)
            // 그럼 계정 연동도 해야하는데?
            // 계정까지 만들고 서버에 계정 데이터도 DB로 저장한다고???
            // 죽겠네
            // 그리고 플레이어 덱이 올바르게 규칙에 맞게 있는지도 검사해야함
            // 구건이 json 수정해서 개사기덱 만들며 어떻게함... -> 이걸 담당하는건 DeckRepository!!!
            // 여기에서 덱 검사까지 하면 catalog를 호출하고, catalog에서는 InitJson 호출때문에 GameContext를 보내야하는데
            // GameContext에서 Catalog를 초기화해야하니깐... 로직이 꼬인다.
            // 그리고 여기선 데이터만 관리하는게 통일성있다.

            await InitEffectDBData();

            await InitPlayerDeckDBDataAsync();
        }

        private static async Task EnsureFileAsync(string sourcePath, string targetPath, bool forceOverwrite = false)
        {
            string directory = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            try
            {
                bool shouldCopy = false;

                if (!File.Exists(targetPath))
                {
                    shouldCopy = true;
                }
                else if (forceOverwrite)
                {
                    shouldCopy = true;
                }

                if (shouldCopy)
                {
                    await FileIntegrity.EnsureJsonIntegrityAsync(sourcePath, targetPath);
                }
                else
                {
                    await FileIntegrity.EnsureJsonIntegrityAsync(sourcePath, targetPath);
                }
            }
            catch (System.IO.IOException ex)
            {
                Debug.LogWarning($"[DataInitializer] 파일 접근 충돌 무시 (다중 실행 중): {ex.Message}");
            }
        }

        public static async Task InitCardDBData()
        {
            bool isEditor = false;
#if UNITY_EDITOR
            isEditor = true; // 에디터에서는 항상 최신 JSON을 반영하도록 설정
#endif
            await EnsureFileAsync(PathConstants.CardDbSourceFilePath, PathConstants.CardDbTargetFilePath, isEditor);
        }

        public static async Task InitSampleDeckDBData()
        {
            bool isEditor = false;
#if UNITY_EDITOR
            isEditor = true;
#endif
            await EnsureFileAsync(PathConstants.SampleDeckDBSourceFilePath, PathConstants.SampleDeckDBTargetFilePath,
                isEditor);
        }

        public static async Task InitEffectDBData()
        {
            // 새 Effect 시스템에서는 cardsEffects.json 단일 파일만 사용한다.
            // 옛 commands/conditions/triggers/schemas는 폐지 (코드 명시 등록 + 스키마 미사용).
            if (!Directory.Exists(PathConstants.EffectTargetFolderPath))
                Directory.CreateDirectory(PathConstants.EffectTargetFolderPath);

            bool isEditor = false;
#if UNITY_EDITOR
            isEditor = true;
#endif

            string source = Path.Combine(PathConstants.EffectSourceFolderPath, "cardsEffects.json");
            string target = Path.Combine(PathConstants.EffectTargetFolderPath, "cardsEffects.json");
            await EnsureFileAsync(source, target, isEditor);
        }

        private static async Task InitPlayerDeckDBDataAsync()
        {
            var targetPath = PathConstants.PlayerDeckTargetFilePath;

            // 플레이어 덱은 세이브 데이터이므로 에디터라고 해서 함부로 덮어쓰면 안 됩니다.
            // 하지만 파일 구조가 깨졌을 때만 새로 생성합니다.
            if (!File.Exists(targetPath))
            {
                await CreateNewPlayerDeckFile(targetPath);
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(targetPath);
                var deckData = JsonConvert.DeserializeObject<PlayerDeck>(json);

                // JSON 키값이 "PlayerDecks"인데 클래스 멤버가 "playerDecks"이면 null이 나올 수 있음
                if (deckData?.playerDecks == null)
                {
                    Debug.LogWarning("[DataInitializer] PlayerDeck 데이터 구조 손상 또는 키값 불일치. 재설정 진행.");
                    await CreateNewPlayerDeckFile(targetPath);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DataInitializer] PlayerDeck 로드 실패: {ex.Message}");
                await CreateNewPlayerDeckFile(targetPath);
            }
        }

        private static async Task CreateNewPlayerDeckFile(string targetPath)
        {
            var initDeckData = new PlayerDeck();
            var json = JsonConvert.SerializeObject(initDeckData, Formatting.Indented);
            await File.WriteAllTextAsync(targetPath, json);
            Debug.Log($"[Init] PlayerDeck 신규 파일 생성: {targetPath}");
        }
    }
}