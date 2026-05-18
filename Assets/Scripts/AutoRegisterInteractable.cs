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

        // ✅ Vérifier si déjà enregistré
        bool alreadyRegistered = interactablesData.Objects
            .Any(o => o.gameObject == gameObject);

        if (alreadyRegistered) return;

        // ✅ Créer le nouvel objet
        ConvaiInteractablesData.Object newObject = new ConvaiInteractablesData.Object()
        {
            Name = string.IsNullOrEmpty(objectName) ? gameObject.name : objectName,
            Description = objectDescription,
            gameObject = gameObject
        };

        // ✅ Redimensionner le tableau et ajouter
        ConvaiInteractablesData.Object[] newArray = 
            new ConvaiInteractablesData.Object[interactablesData.Objects.Length + 1];
        
        interactablesData.Objects.CopyTo(newArray, 0);
        newArray[newArray.Length - 1] = newObject;
        interactablesData.Objects = newArray;

        Debug.Log($"{gameObject.name} ajouté à ConvaiInteractablesData.");
    }

    private void OnDestroy()
    {
        ConvaiInteractablesData interactablesData = FindObjectOfType<ConvaiInteractablesData>();
        if (interactablesData == null) return;

        // ✅ Retirer du tableau
        interactablesData.Objects = interactablesData.Objects
            .Where(o => o.gameObject != gameObject)
            .ToArray();

        Debug.Log($"{gameObject.name} retiré de ConvaiInteractablesData.");
    }
}