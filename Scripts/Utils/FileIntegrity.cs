using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif

namespace Utils
{
    public static class FileIntegrity
    {
        // 파일을 스트림으로 열어 조금씩 읽으며 해시를 계산 (메모리 사용량 최소화)
        public static string ComputeSHA256LowMemory(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath); // 파일 전체를 메모리에 올리지 않음

            var hash = sha.ComputeHash(stream);

            return BytesToString(hash);
        }

        // 이미 메모리에 로드된 데이터(Android StreamingAssets 등)로 해시 계산
        private static string ComputeSHA256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return BytesToString(hash);
        }

        // 바이트 배열을 16진수 문자열로 변환하는 헬퍼 함수
        private static string BytesToString(byte[] hashBytes)
        {
            var sb = new StringBuilder();
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static async Task<byte[]> LoadStreamingAssetsWithRetryAsync(string targetPath, int maxRetries = 3)
        {
            int currentRetry = 0;

            while (true)
            {
                try
                {
#if UNITY_ANDROID
                    using var request = UnityWebRequest.Get(targetPath);
                    
                    var operation = request.SendWebRequest();
                    
                    while (!operation.isDone) await Task.Yield();
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {  
                        return request.downloadHandler.data;
                    }
                    else
                    {
                        throw new Exception($"Download Error: {request.error}");
                    }
#else
                    return await File.ReadAllBytesAsync(targetPath);
#endif
                }
                catch (Exception ex)
                {
                    currentRetry++;
                    if (currentRetry > maxRetries)
                    {
                        Debug.LogError($"최종 실패: {targetPath} - {ex.Message}");
                        throw;
                    }

                    Debug.LogWarning($"재시도 중 ({currentRetry}/{maxRetries})... : {ex.Message}");
                    await Task.Delay(500 * currentRetry);
                }
            }
        }

        public static async Task EnsureJsonIntegrityAsync(string sourcePath, string targetPath)
        {
            string sourceHash = string.Empty;
            byte[] sourceBytes = null;

            // 1. 원본(Source) 해시 계산
#if UNITY_ANDROID
            // 안드로이드는 스트림으로 직접 열 수 없어서 메모리에 로드 후 해시 계산
            sourceBytes = await LoadStreamingAssetsWithRetryAsync(sourcePath);
            sourceHash = ComputeSHA256(sourceBytes);
#else
            // PC/Editor는 스트림으로 열 수 있으므로 LowMemory 방식 사용
            // 단, 나중에 덮어쓸 경우를 대비해 파일이 없거나 다를 때만 Load 합니다.
            sourceHash = ComputeSHA256LowMemory(sourcePath);
#endif

            // 2. 대상(Target) 파일이 없으면 무조건 복사
            if (!File.Exists(targetPath))
            {
                // 소스 데이터가 아직 로드되지 않았다면(PC 경우) 로드
                if (sourceBytes == null) sourceBytes = await File.ReadAllBytesAsync(sourcePath);

                await File.WriteAllBytesAsync(targetPath, sourceBytes);
                Debug.Log($"[FileIntegrity] Initial Copy: {targetPath}");
                return;
            }

            // 3. 대상(Target) 해시 계산 - 여기서는 무조건 LowMemory 사용 가능!
            // 기존에는 LoadPersistentFileAsync로 다 읽었지만, 이제는 스트림으로 해시만 계산
            var targetHash = ComputeSHA256LowMemory(targetPath);

            // 4. 비교 및 덮어쓰기
            if (sourceHash != targetHash)
            {
                Debug.Log($"[FileIntegrity] Update Detected. Source: {sourceHash} vs Target: {targetHash}");

                // 소스 데이터가 로드되지 않았다면 로드
                if (sourceBytes == null)
                {
#if UNITY_ANDROID
                    sourceBytes = await LoadStreamingAssetsWithRetryAsync(sourcePath); // 이미 위에서 했겠지만 안전장치
#else
                    sourceBytes = await File.ReadAllBytesAsync(sourcePath);
#endif
                }

                await File.WriteAllBytesAsync(targetPath, sourceBytes);
                Debug.Log($"[FileIntegrity] Update Complete: {Path.GetFileName(targetPath)}");
            }
            else
            {
                Debug.Log($"[FileIntegrity] File is up to date: {targetPath}");
            }
        }
    }
}