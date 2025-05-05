using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Nostr.Unity.Utils
{
    public static class TaskExtensions
    {
        public static IEnumerator AsCoroutine(this Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                Debug.LogError(task.Exception);
                throw task.Exception;
            }
        }

        public static IEnumerator AsCoroutine<T>(this Task<T> task, System.Action<T> onComplete = null)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                Debug.LogError(task.Exception);
                throw task.Exception;
            }
            onComplete?.Invoke(task.Result);
        }
    }
} 