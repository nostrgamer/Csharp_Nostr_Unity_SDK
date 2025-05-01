using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NNostrUnitySDK
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly object Lock = new object();
        private readonly Queue<Action> _actions = new Queue<Action>();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (Lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("UnityMainThreadDispatcher");
                            _instance = go.AddComponent<UnityMainThreadDispatcher>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            lock (Lock)
            {
                while (_actions.Count > 0)
                {
                    _actions.Dequeue()?.Invoke();
                }
            }
        }

        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (Lock)
            {
                _actions.Enqueue(action);
            }
        }
    }
} 