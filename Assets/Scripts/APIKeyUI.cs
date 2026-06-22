using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Convai.Scripts.Runtime.Utils;

public class APIKeyUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField apiKeyInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Canvas title;
    private void Start()
    {
        confirmButton.onClick.AddListener(() =>
        {
            ConvaiAPIKeySetup apiKeySetup = Resources.Load<ConvaiAPIKeySetup>("ConvaiAPIKey");
            ///if (apiKeySetup != null)
            //{
                apiKeySetup.APIKey = apiKeyInput.text;
        
                // Vérifie que la valeur a bien été changée
                Debug.Log("Nouvelle API Key : " + apiKeySetup.APIKey);
                Debug.Log("Correspond à l'input : " + (apiKeySetup.APIKey == apiKeyInput.text));
            //}
            //else
            //{
            //    Debug.LogError("ConvaiAPIKeySetup introuvable dans Resources !");
            //}
        });

    }
    
}