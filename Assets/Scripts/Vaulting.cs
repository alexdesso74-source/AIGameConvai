using System.Collections;
using Supercyan.FreeSample;
using UnityEngine;

public class Vaulting : MonoBehaviour
{
    [SerializeField] private Vector3 forwardRayOffset;
    
    [SerializeField] private float forwardRayLength;
    [SerializeField] private float heightRayLength;
    
    [SerializeField] private LayerMask obstacleMask;
    
    private RaycastHit forwardHitData;
    private RaycastHit heightHitData;
    
    [SerializeField] private SimpleSampleCharacterControl playerController;
    [SerializeField] private VaultAction vaultAction;
    
    private bool _isVaulting = false;

    private void Update()
    {
        if (!_isVaulting && ObstacleCheck())
        {
            Debug.Log("Obstacle détecté");
        
            if (vaultAction == null)
            {
                Debug.LogError("VaultAction est null ! Assigne-le dans l'Inspector.");
                return;
            }

            if (vaultAction.CanVault(forwardHitData, heightHitData, transform))
            {
                Debug.Log("CanVault = true, démarrage du vault");
                StartCoroutine(VaultAction());
            }
            else
            {
                Debug.Log($"CanVault = false" +
                          $"\nTag obstacle : {forwardHitData.transform.tag}" +
                          $"\nHauteur : {heightHitData.point.y - transform.position.y}" +
                          $"\nObstacle touché : {forwardHitData.transform.name}");
            }
        }
    }

    private IEnumerator VaultAction()
    {
        _isVaulting = true;

        MatchTargetParameters matchTargetParams = null;

        if (vaultAction.EnableTargetMatching)
        {
            matchTargetParams = new MatchTargetParameters()
            {
                matchPos = vaultAction.MatchPos,
                matchBodyPart = vaultAction.MatchBodyPart,
                matchPosWeight = vaultAction.MatchPosWeight,
                matchStartTime = vaultAction.MatchStartTime,
                matchTargetTime = vaultAction.MatchTargetTime
            };
        }

        yield return playerController.PerformVaulting(vaultAction.AnimName, matchTargetParams,
            vaultAction.TargetRotation, vaultAction.RotateTowradsObstacle,
            vaultAction.MirrorActionAnimation);

        _isVaulting = false;
    }
    
    public bool ObstacleCheck()
    {
        var forwardOrigin = transform.position + forwardRayOffset;

        bool obstacleDetected = Physics.Raycast(forwardOrigin, transform.forward, out forwardHitData,
            forwardRayLength, obstacleMask);

        Debug.DrawRay(forwardOrigin, transform.forward * forwardRayLength,
            obstacleDetected ? Color.green : Color.red);

        bool obstacleHeightDetected = false;

        if (obstacleDetected)
        {
            var heightOrigin = forwardHitData.point + Vector3.up * heightRayLength;

            obstacleHeightDetected = Physics.Raycast(heightOrigin, Vector3.down, out heightHitData,
                heightRayLength, obstacleMask);

            Debug.DrawRay(heightOrigin, Vector3.down * heightRayLength,
                obstacleHeightDetected ? Color.green : Color.red);
        }

        return obstacleDetected && obstacleHeightDetected;
    }
    
}

public class MatchTargetParameters
{
    public Vector3 matchPos;
    public Vector3 matchPosWeight;
    
    public AvatarTarget matchBodyPart;
    
    public float matchStartTime;
    public float matchTargetTime;
    
    
    
}
