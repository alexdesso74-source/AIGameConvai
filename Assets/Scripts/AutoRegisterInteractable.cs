using Convai.Scripts.Runtime.Features;
using System.Linq;
using UnityEngine;

public class AutoRegisterInteractable : MonoBehaviour
{
    [SerializeField] private string objectName;
    [SerializeField] private string objectDescription;

    private void Start()
    {
        ConvaiInteractablesData interactablesData = FindObjectOfType<ConvaiInteractablesData>();

        if (interactablesData == null)
        {
            Debug.LogError("ConvaiInteractablesData not found in scene!");
            return;
        }


        bool alreadyRegistered = interactablesData.Objects
            .Any(o => o.gameObject == gameObject);

        if (alreadyRegistered) return;


        ConvaiInteractablesData.Object newObject = new ConvaiInteractablesData.Object()
        {
            Name = string.IsNullOrEmpty(objectName) ? gameObject.name : objectName,
            Description = objectDescription,
            gameObject = gameObject
        };


        ConvaiInteractablesData.Object[] newArray = 
            new ConvaiInteractablesData.Object[interactablesData.Objects.Length + 1];
        
        interactablesData.Objects.CopyTo(newArray, 0);
        newArray[newArray.Length - 1] = newObject;
        interactablesData.Objects = newArray;

        Debug.Log($"{gameObject.name} add to ConvaiInteractablesData.");
    }

    private void OnDestroy()
    {
        ConvaiInteractablesData interactablesData = FindObjectOfType<ConvaiInteractablesData>();
        if (interactablesData == null) return;


        interactablesData.Objects = interactablesData.Objects
            .Where(o => o.gameObject != gameObject)
            .ToArray();

        Debug.Log($"{gameObject.name} remove from ConvaiInteractablesData.");
    }
}