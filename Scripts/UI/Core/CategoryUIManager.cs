using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace UI.Core
{
    public class CategoryUIManager : MonoBehaviour
    {
        [Header("UI References")]
        public Transform contentParent;
        public GameObject[] itemPrefabs;

        [Header("Interaction Control")]
        private readonly List<GameObject> activeObjects = new();
        private readonly Dictionary<GameObject, int> objectToPoolIndex = new();

        private ObjectPool<GameObject>[] pools;

        private void Awake()
        {
            InitializePools();
        }

        private void InitializePools()
        {
            pools = new ObjectPool<GameObject>[itemPrefabs.Length];

            for (var i = 0; i < itemPrefabs.Length; i++)
            {
                var index = i;

                pools[i] = new ObjectPool<GameObject>(
                    () =>
                    {
                        var obj = Instantiate(itemPrefabs[index], contentParent, false);
                        return obj;
                    },
                    obj =>
                    {
                        obj.SetActive(true);
                        obj.transform.SetAsLastSibling();
                        obj.transform.localScale = Vector3.one;

                        var rectTransform = obj.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            rectTransform.anchoredPosition3D = Vector3.zero;
                            rectTransform.localRotation = Quaternion.identity;
                        }
                    },
                    obj => obj.SetActive(false),
                    Destroy,
                    false,
                    10,
                    50
                );
            }
        }

        public GameObject ChangeContent(int categoryIndex)
        {
            foreach (var obj in activeObjects)
            {
                var poolIndex = objectToPoolIndex[obj];
                pools[poolIndex].Release(obj);
            }

            activeObjects.Clear();
            objectToPoolIndex.Clear();

            if (categoryIndex < 0 || categoryIndex >= pools.Length)
            {
                return null;
            }

            var newObj = pools[categoryIndex].Get();
            activeObjects.Add(newObj);
            objectToPoolIndex.Add(newObj, categoryIndex);

            return newObj;
        }
    }
}