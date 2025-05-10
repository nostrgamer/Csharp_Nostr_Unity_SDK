using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace NostrUnity.Utils
{
    /// <summary>
    /// Utility class for JSON operations
    /// </summary>
    public static class JsonUtils
    {
        /// <summary>
        /// Serialize an object to JSON string
        /// </summary>
        /// <param name="obj">Object to serialize</param>
        /// <returns>JSON string</returns>
        public static string Serialize(object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error serializing to JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deserialize JSON string to object
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="json">JSON string</param>
        /// <returns>Deserialized object</returns>
        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing from JSON: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Try to parse a JSON string as an array of objects
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <returns>Array of objects or null if parsing fails</returns>
        public static object[] TryParseJsonArray(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<object[]>(json);
            }
            catch
            {
                return null;
            }
        }
    }
} 