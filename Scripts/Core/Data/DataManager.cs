using System;
using System.Collections;
using System.Collections.Generic;
using Core.Attributes;
using Core.Data.Enums;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Core.Data
{
    // JSON의 최상위 구조(totalCount, data 배열)를 매핑하는 래퍼 클래스
    [Serializable]
    public class BaseResponse<T>
    {
        [JsonProperty("totalCount")]
        public int totalCount;

        [JsonProperty("data")]
        public List<T> data;
    }

    public class DataManager<T> : MonoBehaviour
    {
        public AppMode currentMode = AppMode.Local;

        [ShowIf("currentMode == Server")]
        [SerializeField] private string serverPath;

        // [핵심 변경 사항] string 경로 대신 TextAsset을 사용하여 인스펙터에서 직접 할당받습니다.
        [ShowIf("currentMode == Local")]
        [SerializeField] private TextAsset localJsonFile;

        public List<T> dataList = new();

        public int TotalCount { get; private set; }

        public int Length => dataList.Count;

        public void FetchDataLocal()
        {
            // 인스펙터에 파일이 잘 할당되었는지 확인합니다.
            if (localJsonFile != null)
            {
                // TextAsset에서 바로 JSON 문자열을 읽어옵니다.
                var jsonText = localJsonFile.text;

                // BaseResponse<T>를 사용하여 역직렬화
                var response = JsonConvert.DeserializeObject<BaseResponse<T>>(jsonText);

                if (response != null && response.data != null)
                {
                    TotalCount = response.totalCount;
                    dataList.AddRange(response.data);

                    Debug.Log($"[DataManager] 로컬 데이터 로드 성공: {localJsonFile.name} (총 {Length}개)");
                }
            }
            else
            {
                // 파일이 할당되지 않았을 때의 에러 처리
                Debug.LogError($"[DataManager] JSON 파일 누락: 인스펙터에 localJsonFile이 할당되지 않았습니다. ({gameObject.name})");
            }
        }

        public IEnumerator FetchDataServer(int page, int limit)
        {
            var url = $"{serverPath}?page={page}&limit={limit}";

            using (var uwr = UnityWebRequest.Get(url))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    var jsonResponse = uwr.downloadHandler.text;

                    // BaseResponse<T>를 사용하여 역직렬화
                    var response = JsonConvert.DeserializeObject<BaseResponse<T>>(jsonResponse);

                    if (response != null && response.data != null)
                    {
                        TotalCount = response.totalCount;
                        dataList.AddRange(response.data);
                    }
                }
                else
                {
                    Debug.LogError("[DataManager] 네트워크 오류: " + uwr.error);
                }
            }
        }
    }
}