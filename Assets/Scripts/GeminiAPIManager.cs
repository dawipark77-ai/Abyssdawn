using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Genesis01
{
    public class GeminiAPIManager : MonoBehaviour
    {
        [Header("Gemini API Settings")]
        [Tooltip("Enter your Google AI Studio API Key here")]
        [SerializeField] private string apiKey = "YOUR_API_KEY_HERE";
        // [2026-05-24] gemini-1.5-flash가 v1beta에서 deprecated되어 404 발생.
        // 현행 가능한 모델: gemini-2.5-flash(권장), gemini-2.5-pro, gemini-2.0-flash, gemini-flash-latest
        [Tooltip("사용할 Gemini 모델. 기본: gemini-2.5-flash. 다른 옵션: gemini-2.5-pro / gemini-2.0-flash / gemini-flash-latest")]
        [SerializeField] private string modelName = "gemini-2.5-flash";

        private const string ApiUrlFormat = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

        public static GeminiAPIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void GenerateContent(string prompt, Action<string> onSuccess, Action<string> onError = null)
        {
            StartCoroutine(PostRequest(prompt, onSuccess, onError));
        }

        private IEnumerator PostRequest(string prompt, Action<string> onSuccess, Action<string> onError)
        {
            string url = string.Format(ApiUrlFormat, modelName, apiKey);

            // Create JSON body
            var requestBody = new GeminiRequest
            {
                contents = new Content[]
                {
                    new Content
                    {
                        parts = new Part[]
                        {
                            new Part { text = prompt }
                        }
                    }
                }
            };

            string jsonBody = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Gemini API Error: {request.error}\nResponse: {request.downloadHandler.text}");
                    onError?.Invoke(request.error);
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;
                    try
                    {
                        GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(jsonResponse);
                        if (response.candidates != null && response.candidates.Length > 0)
                        {
                            string text = response.candidates[0].content.parts[0].text;
                            onSuccess?.Invoke(text);
                        }
                        else
                        {
                            onError?.Invoke("No candidates found in response.");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"JSON Parse Error: {e.Message}");
                        onError?.Invoke(e.Message);
                    }
                }
            }
        }

        // Data classes for JSON serialization
        [Serializable]
        private class GeminiRequest
        {
            public Content[] contents;
        }

        [Serializable]
        private class Content
        {
            public Part[] parts;
        }

        [Serializable]
        private class Part
        {
            public string text;
        }

        [Serializable]
        private class GeminiResponse
        {
            public Candidate[] candidates;
        }

        [Serializable]
        private class Candidate
        {
            public Content content;
        }
    }
}















