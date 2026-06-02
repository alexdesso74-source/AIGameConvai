using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.LoggerSystem;
using Service;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Convai.Scripts.Runtime.Features
{
    // STEP 1: Add the enum for your custom action here. 
    public enum ActionChoice
    {
        None,
        Jump,
        Crouch,
        MoveTo,
        PickUp,
        Drop,
        Throw,
        PassThrough,
        Sit,
        StandUp
    }

    /// <summary>
    ///     DISCLAIMER: The action API is in experimental stages and can misbehave. Meanwhile, feel free to try it out and play
    ///     around with it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Convai Actions Handler")]
    public class ConvaiActionsHandler : MonoBehaviour
    {
        [SerializeField] public ActionMethod[] actionMethods;
        public List<string> actionResponseList = new();
        private readonly List<ConvaiAction> _actionList = new();
        public readonly ActionConfig ActionConfig = new();
        private List<string> _actions = new();
        private ConvaiNPC _currentNPC;
        private ConvaiInteractablesData _interactablesData;
        private Coroutine _playActionListCoroutine;
        public bool IsVaulting = false;
        private bool isSitting = false;
        private GameObject _lastTarget;
        // Awake is called when the script instance is being loaded
        private void Awake()
        {
            // Find the global action settings object in the scene
            _interactablesData = FindObjectOfType<ConvaiInteractablesData>();

            // Check if the global action settings object is missing
            if (_interactablesData == null)
                // Log an error message to indicate missing Convai Action Settings
                ConvaiLogger.Error("Convai Action Settings missing. Please create a game object that handles actions.",
                    ConvaiLogger.LogCategory.Character);

            // Check if this GameObject has a ConvaiNPC component attached
            if (TryGetComponent(out ConvaiNPC npc))
                // If it does, set the current NPC to this GameObject
                _currentNPC = npc;


            // Iterate through each action method and add its name to the action configuration
            foreach (ActionMethod actionMethod in actionMethods) ActionConfig.Actions.Add(actionMethod.action);

            if (_interactablesData != null)
            {
                // Iterate through each character in global action settings and add them to the action configuration
                foreach (ConvaiInteractablesData.Character character in _interactablesData.Characters)
                {
                    ActionConfig.Types.Character rpcCharacter = new()
                    {
                        Name = character.Name,
                        Bio = character.Bio
                    };

                    ActionConfig.Characters.Add(rpcCharacter);
                }

                // Iterate through each object in global action settings and add them to the action configuration
                foreach (ConvaiInteractablesData.Object eachObject in _interactablesData.Objects)
                {
                    ActionConfig.Types.Object rpcObject = new()
                    {
                        Name = eachObject.Name,
                        Description = eachObject.Description
                    };
                    ActionConfig.Objects.Add(rpcObject);
                }
            }
        }

        private void Reset()
        {
            actionMethods = new ActionMethod[]
            {
                new() { action = "Move To", actionChoice = ActionChoice.MoveTo },
                new() { action = "Pick Up", actionChoice = ActionChoice.PickUp },
                new() { action = "Dance", animationName = "Dance", actionChoice = ActionChoice.None },
                new() { action = "Drop", actionChoice = ActionChoice.Drop },
                new() { action = "Crouch", actionChoice = ActionChoice.Crouch },
                new() { action = "Jump", actionChoice = ActionChoice.Jump },
                new() { action = "Throw", actionChoice = ActionChoice.Throw },
                new() { action = "Pass Through", actionChoice = ActionChoice.PassThrough },
                new() { action = "Sit", actionChoice = ActionChoice.Sit },
                new() { action = "Stand Up", actionChoice = ActionChoice.StandUp }
            };
        }

        // Start is called before the first frame update
        private void Start()
        {
            // Set up the action configuration

            #region Actions Setup

            // Set the classification of the action configuration to "multistep"
            ActionConfig.Classification = "multistep";

            // Log the configured action information
            ConvaiLogger.DebugLog(ActionConfig, ConvaiLogger.LogCategory.Actions);

            #endregion

            // Start playing the action list using a coroutine
            _playActionListCoroutine = StartCoroutine(PlayActionList());
        }

        private void OnEnable()
        {
            if (_playActionListCoroutine != null)
            {
                _playActionListCoroutine = StartCoroutine(PlayActionList());
            }
        }

        private void OnDisable()
        {
            if (_playActionListCoroutine != null)
            {
                StopCoroutine(_playActionListCoroutine);
            }
        }

        private void Update()
        {
            if (actionResponseList.Count > 0)
            {
                ParseActions(actionResponseList[0]);
                actionResponseList.RemoveAt(0);
            }
        }

        private void ParseActions(string actionsString)
        {
            actionsString = actionsString.Trim();
            ConvaiLogger.DebugLog($"Parsing actions from: {actionsString}", ConvaiLogger.LogCategory.Actions);

            _actions = actionsString.Split(", ").ToList();
            _actionList.Clear();

            foreach (string action in _actions)
            {
                List<string> actionWords = action.Split(' ').ToList();
                ConvaiLogger.Info($"Processing action: {action}", ConvaiLogger.LogCategory.Actions);
                ParseSingleAction(actionWords);
            }
        }

        /// <summary>
        ///     Parses a single action from a list of action words.
        /// </summary>
        /// <param name="actionWords">The list of words representing the action.</param>
        private void ParseSingleAction(List<string> actionWords)
        {
            for (int j = 0; j < actionWords.Count; j++)
            {
                // Split the action into verb and object parts
                string[] verbPart = actionWords.Take(j + 1).ToArray();
                string[] objectPart = actionWords.Skip(j + 1).ToArray();

                // Remove trailing 's' from verb words
                verbPart = verbPart.Select(word => word.TrimEnd('s')).ToArray();
                string actionString = string.Join(" ", verbPart);

                // Find the best matching action using Levenshtein distance
                ActionMethod matchingAction = actionMethods
                    .OrderBy(a => LevenshteinDistance(a.action.ToLower(), actionString.ToLower()))
                    .FirstOrDefault();

                // If no close match is found, continue to the next iteration
                if (matchingAction == null ||
                    LevenshteinDistance(matchingAction.action.ToLower(), actionString.ToLower()) > 2) continue;

                // Find the target object for the action
                GameObject targetObject = FindTargetObject(objectPart);
                LogActionResult(verbPart, objectPart, targetObject);

                if (targetObject == null && _lastTarget != null)
                    targetObject = _lastTarget;
                else if (targetObject != null)
                    _lastTarget = targetObject;
                
                // Add the parsed action to the action list
                //Debug.Log($"Action parsée : {matchingAction.actionChoice} | Target : {targetObject?.name ?? "NULL"}");
                _actionList.Add(new ConvaiAction(matchingAction.actionChoice, targetObject,
                    matchingAction.animationName));
                break;
            }
        }

        /// <summary>
        ///     Finds the target object based on the object part of the action.
        /// </summary>
        /// <param name="objectPart">The array of words representing the object.</param>
        /// <returns>The GameObject that best matches the object description, or null if no match is found.</returns>
        private GameObject FindTargetObject(string[] objectPart)
        {
            string targetName = string.Join(" ", objectPart);

            // Try to find a matching object
            ConvaiInteractablesData.Object obj = _interactablesData.Objects
                .OrderBy(o => LevenshteinDistance(o.Name.ToLower(), targetName.ToLower()))
                .FirstOrDefault();

            if (obj != null && LevenshteinDistance(obj.Name.ToLower(), targetName.ToLower()) <= 2)
                return obj.gameObject;

            // If no object is found, try to find a matching character
            ConvaiInteractablesData.Character character = _interactablesData.Characters
                .OrderBy(c => LevenshteinDistance(c.Name.ToLower(), targetName.ToLower()))
                .FirstOrDefault();

            if (character != null && LevenshteinDistance(character.Name.ToLower(), targetName.ToLower()) <= 2)
                return character.gameObject;

            return null;
        }

        /// <summary>
        ///     Calculates the Levenshtein distance between two strings.
        /// </summary>
        /// <param name="s">The first string.</param>
        /// <param name="t">The second string.</param>
        /// <returns>The Levenshtein distance between the two strings.</returns>
        private int LevenshteinDistance(string s, string t)
        {
            int[][] d = new int[s.Length + 1][];
            for (int index = 0; index < s.Length + 1; index++) d[index] = new int[t.Length + 1];

            // Initialize the first row and column
            for (int i = 0; i <= s.Length; i++)
                d[i][0] = i;
            for (int j = 0; j <= t.Length; j++)
                d[0][j] = j;

            // Calculate the distance
            for (int j = 1; j <= t.Length; j++)
            for (int i = 1; i <= s.Length; i++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i][j] = Math.Min(Math.Min(d[i - 1][j] + 1, d[i][j - 1] + 1), d[i - 1][j - 1] + cost);
            }

            return d[s.Length][t.Length];
        }


        private void LogActionResult(string[] verbPart, string[] objectPart, GameObject targetObject)
        {
            string verb = string.Join(" ", verbPart).ToLower();
            string obj = string.Join(" ", objectPart).ToLower();

            if (targetObject != null)
            {
                ConvaiLogger.DebugLog($"Active Target: {obj}", ConvaiLogger.LogCategory.Actions);
                ConvaiLogger.DebugLog($"Found matching target: {targetObject.name} for action: {verb}",
                    ConvaiLogger.LogCategory.Actions);
            }
            else
            {
                ConvaiLogger.Warn($"No matching target found for action: {verb}", ConvaiLogger.LogCategory.Actions);
            }
        }


        /// <summary>
        ///     Event that is triggered when an action starts.
        /// </summary>
        /// <remarks>
        ///     This event can be subscribed to in order to perform custom logic when an action starts.
        ///     The event provides the name of the action and the GameObject that the action is targeting.
        /// </remarks>
        public event Action<string, GameObject> ActionStarted;

        /// <summary>
        ///     Event that is triggered when an action ends.
        /// </summary>
        /// <remarks>
        ///     This event can be subscribed to in order to perform custom logic when an action ends.
        ///     The event provides the name of the action and the GameObject that the action was targeting.
        /// </remarks>
        public event Action<string, GameObject> ActionEnded;

        /// <summary>
        ///     This coroutine handles playing the actions in the action list.
        /// </summary>
        /// <returns></returns>
        private IEnumerator PlayActionList()
        {
            while (true)
                // Check if there are actions in the action list
                if (_actionList.Count > 0)
                {
                    // Call the DoAction function for the first action in the list and wait until it's done
                    yield return DoAction(_actionList[0]);

                    // Remove the completed action from the list
                    _actionList.RemoveAt(0);
                }
                else
                {
                    // If there are no actions in the list, yield to wait for the next frame
                    yield return null;
                }
        }

        private IEnumerator DoAction(ConvaiAction action)
        {
            // STEP 2: Add the function call for your action here corresponding to your enum.
            //         Remember to yield until its return if it is an Enumerator function.
            Debug.Log($"DoAction : {action.Verb}");
            // Use a switch statement to handle different action choices based on the ActionChoice enum
            switch (action.Verb)
            {
                case ActionChoice.MoveTo:
                    // Call the MoveTo function and yield until it's completed
                    yield return MoveTo(action.Target);
                    break;

                case ActionChoice.PickUp:
                    // Call the PickUp function and yield until it's completed
                    yield return PickUp(action.Target);
                    break;

                case ActionChoice.Drop:
                    // Call the Drop function
                    Drop(action.Target);
                    break;

                case ActionChoice.Jump:
                    // Call the Jump function
                    yield return Jump();
                    break;

                case ActionChoice.Crouch:
                    // Call the Crouch function and yield until it's completed
                    yield return Crouch();
                    break;

                case ActionChoice.Throw:
                    // Call the Throw function and yield until it's completed
                    yield return Throw(action.Target);
                    break;
                case ActionChoice.PassThrough:
                    // Call the PassThrough function and yield until it's completed
                    yield return PassThroughAndMoveTo(action.Target);
                    break;
                
                case ActionChoice.Sit:
                    // Call the Sit function and yield until it's completed
                    yield return Sit(action.Target);
                    break;
                
                case ActionChoice.StandUp:
                    // Call the StandUp function and yield until it's completed
                    yield return StandUp();
                    break;

                case ActionChoice.None:
                    // Call the AnimationActions function and yield until it's completed
                    yield return AnimationActions(action.Animation);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();


            }

            // Yield once to ensure the coroutine advances to the next frame
            yield return null;
        }

        /// <summary>
        ///     This method is a coroutine that handles playing an animation for Convai NPC.
        ///     The method takes in the name of the animation to be played as a string parameter.
        /// </summary>
        /// <param name="animationName"> The name of the animation to be played. </param>
        /// <returns> A coroutine that plays the animation. </returns>
        private IEnumerator AnimationActions(string animationName)
        {
            // Logging the action of initiating the animation with the provided animation name.
            ConvaiLogger.DebugLog("Doing animation: " + animationName, ConvaiLogger.LogCategory.Actions);

            // Attempting to get the Animator component attached to the current NPC object.
            // The Animator component is responsible for controlling animations on the GameObject.
            Animator animator = _currentNPC.GetComponent<Animator>();

            // Converting the provided animation name to its corresponding hash code.
            // This is a more efficient way to refer to animations and Animator states.
            int animationHash = Animator.StringToHash(animationName);

            // Check if the Animator component has a state with the provided hash code.
            // This is a safety check to prevent runtime errors if the animation is not found.
            if (!animator.HasState(0, animationHash))
            {
                // Logging a message to indicate that the animation was not found.
                ConvaiLogger.DebugLog("Could not find an animator state named: " + animationName,
                    ConvaiLogger.LogCategory.Actions);

                // Exiting the coroutine early since the animation is not available.
                yield break;
            }

            // Playing the animation with a cross-fade transition.
            // The second parameter '0.1f' specifies the duration of the cross-fade.
            animator.CrossFadeInFixedTime(animationHash, 0.1f);

            // Waiting for a short duration (just over the cross-fade time) to allow the animation transition to start.
            // This ensures that subsequent code runs after the animation has started playing.
            yield return new WaitForSeconds(0.11f);

            // Getting information about the current animation clip that is playing.
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);

            // Checking if there is no animation clip information available.
            if (clipInfo == null || clipInfo.Length == 0)
            {
                // Logging a message to indicate that there are no animation clips associated with the state.
                ConvaiLogger.DebugLog("Animator state named: " + animationName + " has no associated animation clips",
                    ConvaiLogger.LogCategory.Actions);

                // Exiting the coroutine as there is no animation to play.
                yield break;
            }

            // Defining variables to store the length and name of the animation clip.
            float length = 0;
            string animationClipName = "";

            // Iterating through the array of animation clips to find the one that is currently playing.
            foreach (AnimatorClipInfo clipInf in clipInfo)
            {
                // Logging the name of the animation clip for debugging purposes.
                ConvaiLogger.DebugLog("Clip name: " + clipInf.clip.name, ConvaiLogger.LogCategory.Actions);

                // Storing the current animation clip in a local variable for easier access.
                AnimationClip clip = clipInf.clip;

                // Checking if the animation clip is valid.
                if (clip != null)
                {
                    // Storing the length and name of the animation clip.
                    length = clip.length;
                    animationClipName = clip.name;

                    // Exiting the loop as we've found the information we need.
                    break;
                }
            }

            // Checking if a valid animation clip was found.
            if (length > 0.0f)
            {
                // Logging a message indicating that the animation is now playing.
                ConvaiLogger.DebugLog(
                    "Playing the animation " + animationClipName + " from the Animator State " + animationName +
                    " for " + length + " seconds", ConvaiLogger.LogCategory.Actions);

                // Waiting for the duration of the animation to allow it to play out.
                yield return new WaitForSeconds(length);
            }
            else
            {
                // Logging a message to indicate that no valid animation clips were found or their length was zero.
                ConvaiLogger.DebugLog(
                    "Animator state named: " + animationName +
                    " has no valid animation clips or they have a length of 0", ConvaiLogger.LogCategory.Actions);

                // Exiting the coroutine early.
                yield break;
            }

            // Transitioning back to the idle animation.
            // It is assumed that an "Idle" animation exists and is set up in your Animator Controller.
            animator.CrossFadeInFixedTime(Animator.StringToHash("Idle"), 0.1f);

            // Yielding to wait for one frame to ensure that the coroutine progresses to the next frame.
            // This is often done at the end of a coroutine to prevent issues with Unity's execution order.
            yield return null;
        }

        /// <summary>
        ///     Registers the provided methods to the ActionStarted and ActionEnded events.
        ///     This allows external code to subscribe to these events and react when they are triggered.
        /// </summary>
        /// <param name="onActionStarted">
        ///     The method to be called when an action starts. It should accept a string (the action
        ///     name) and a GameObject (the target of the action).
        /// </param>
        /// <param name="onActionEnded">
        ///     The method to be called when an action ends. It should accept a string (the action name)
        ///     and a GameObject (the target of the action).
        /// </param>
        public void RegisterForActionEvents(Action<string, GameObject> onActionStarted,
            Action<string, GameObject> onActionEnded)
        {
            ActionStarted += onActionStarted;
            ActionEnded += onActionEnded;
        }

        /// <summary>
        ///     Unregisters the provided methods from the ActionStarted and ActionEnded events.
        ///     This allows external code to unsubscribe from these events when they are no longer interested in them.
        /// </summary>
        /// <param name="onActionStarted">
        ///     The method to be removed from the ActionStarted event. It should be the same method that
        ///     was previously registered.
        /// </param>
        /// <param name="onActionEnded">
        ///     The method to be removed from the ActionEnded event. It should be the same method that was
        ///     previously registered.
        /// </param>
        public void UnregisterForActionEvents(Action<string, GameObject> onActionStarted,
            Action<string, GameObject> onActionEnded)
        {
            ActionStarted -= onActionStarted;
            ActionEnded -= onActionEnded;
        }

        [Serializable]
        public class ActionMethod
        {
            [FormerlySerializedAs("Action")] [SerializeField]
            public string action;

            [SerializeField] public string animationName;
            [SerializeField] public ActionChoice actionChoice;
        }

        private class ConvaiAction
        {
            public ConvaiAction(ActionChoice verb, GameObject target, string animation)
            {
                Verb = verb;
                Target = target;
                Animation = animation;
            }

            #region 04. Public variables

            public readonly string Animation;
            public readonly GameObject Target;
            public readonly ActionChoice Verb;

            #endregion
        }

        // STEP 3: Add the function for your action here.

        #region Action Implementation Methods

        /// <summary>
        ///     Coroutine to make the NPC crouch, shrink the collider, play the animation, then return to idle.
        /// </summary>
        private IEnumerator Crouch()
        {
            // Notify listeners that the Crouch action has started
            ActionStarted?.Invoke("Crouch", _currentNPC.gameObject);

            ConvaiLogger.DebugLog("Crouching!", ConvaiLogger.LogCategory.Actions);

            // Retrieve the Animator component from the current NPC
            Animator animator = _currentNPC.GetComponent<Animator>();

            // Play the crouch animation with a cross-fade transition
            animator.CrossFadeInFixedTime(Animator.StringToHash("Crouch"), 0.1f);

            // Wait for the Animator to transition to the crouch state
            yield return new WaitForSeconds(0.11f);

            // Get the length of the crouch animation clip
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo == null || clipInfo.Length == 0)
            {
                ConvaiLogger.DebugLog("No animation clips found for crouch state!", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            float length = clipInfo[0].clip.length;

            // Shrink the first CapsuleCollider to match the crouched height
            _currentNPC.GetComponents<CapsuleCollider>()[0].height = 1.2f;
            _currentNPC.GetComponents<CapsuleCollider>()[0].center = new Vector3(0, 0.6f, 0);

            // Shrink the second CapsuleCollider if it exists (Convai uses two colliders)
            if (_currentNPC.GetComponents<CapsuleCollider>().Length > 1)
            {
                _currentNPC.GetComponents<CapsuleCollider>()[1].height = 1.2f;
                _currentNPC.GetComponents<CapsuleCollider>()[1].center = new Vector3(0, 0.6f, 0);
            }

            // Wait for the crouch animation to finish
            yield return new WaitForSeconds(length);

            // Restore the CapsuleCollider to its standing height
            _currentNPC.GetComponents<CapsuleCollider>()[0].height = 1.8f;
            _currentNPC.GetComponents<CapsuleCollider>()[0].center = new Vector3(0, 0.9f, 0);

            if (_currentNPC.GetComponents<CapsuleCollider>().Length > 1)
            {
                _currentNPC.GetComponents<CapsuleCollider>()[1].height = 1.8f;
                _currentNPC.GetComponents<CapsuleCollider>()[1].center = new Vector3(0, 0.9f, 0);
            }

            // Transition back to the idle animation
            animator.CrossFadeInFixedTime(Animator.StringToHash("Idle"), 0.1f);

            yield return null;

            // Notify listeners that the Crouch action has ended
            ActionEnded?.Invoke("Crouch", _currentNPC.gameObject);
        }

        /// <summary>
        ///     Coroutine to move the NPC to a target GameObject using the NavMeshAgent.
        /// </summary>
        /// <param name="target">The target GameObject to move to.</param>
        private IEnumerator MoveTo(GameObject target)
        {
            // Validate the target before attempting to move
            if (!IsTargetValid(target)) yield break;

            ConvaiLogger.DebugLog($"Moving to Target: {target.name}", ConvaiLogger.LogCategory.Actions);
            ActionStarted?.Invoke("MoveTo", target);

            Animator animator = _currentNPC.GetComponent<Animator>();
            NavMeshAgent navMeshAgent = _currentNPC.GetComponent<NavMeshAgent>();

            // Set up the walking animation and navigation settings
            SetupAnimationAndNavigation(animator, navMeshAgent);

            // Calculate and set the destination
            Vector3 targetDestination = CalculateTargetDestination(target);
            navMeshAgent.SetDestination(targetDestination);
            yield return null;

            // Wait until the NPC reaches the target
            yield return MoveTowardsTarget(target, navMeshAgent);

            // Clean up after movement is complete
            FinishMovement(animator, target);
        }

        /// <summary>
        ///     Validates whether the target is reachable via the NavMesh.
        /// </summary>
        /// <param name="target">The target GameObject to validate.</param>
        /// <returns>True if the target is valid and reachable, false otherwise.</returns>
        private bool IsTargetValid(GameObject target)
        {
            // Check if the target exists and is active in the scene
            if (target == null || !target.activeInHierarchy)
            {
                ConvaiLogger.DebugLog("MoveTo target is null or inactive.", ConvaiLogger.LogCategory.Actions);
                return false;
            }

            NavMeshAgent navMeshAgent = _currentNPC.GetComponent<NavMeshAgent>();
            Vector3 destination = CalculateTargetDestination(target);

            // Check that the destination is on the NavMesh within a 2-unit radius
            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                ConvaiLogger.DebugLog($"Destination of {target.name} is off the NavMesh.", ConvaiLogger.LogCategory.Actions);
                return false;
            }

            // Calculate the full path to verify it is reachable
            NavMeshPath path = new NavMeshPath();
            navMeshAgent.CalculatePath(hit.position, path);

            if (path.status == NavMeshPathStatus.PathInvalid || path.status == NavMeshPathStatus.PathPartial)
            {
                ConvaiLogger.DebugLog($"Path to {target.name} is unreachable.", ConvaiLogger.LogCategory.Actions);
                _currentNPC.SendTextDataAsync(
                    $"You cannot reach {target.name}, there is an obstacle blocking the path. Tell the player.");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Sets up the walking animation and disables automatic NavMesh rotation so we can handle it manually.
        /// </summary>
        private void SetupAnimationAndNavigation(Animator animator, NavMeshAgent navMeshAgent)
        {
            // Play the walking animation
            animator.CrossFade(Animator.StringToHash("Walking"), 0.01f);
            // Disable root motion so the NavMeshAgent drives movement
            animator.applyRootMotion = false;
            // Disable automatic rotation so we can rotate manually towards movement direction
            navMeshAgent.updateRotation = false;
        }

        /// <summary>
        ///     Calculates the destination position snapped to the nearest point on the NavMesh.
        /// </summary>
        /// <param name="target">The target GameObject.</param>
        /// <returns>The closest valid NavMesh position near the target.</returns>
        private Vector3 CalculateTargetDestination(GameObject target)
        {
            Vector3 targetDestination = target.transform.position;

            // Snap the destination to the nearest point on the NavMesh within a 2-unit radius
            if (NavMesh.SamplePosition(targetDestination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return targetDestination;
        }

        /// <summary>
        ///     Coroutine that waits for the NPC to reach the target, rotating towards the movement direction each frame.
        /// </summary>
        private IEnumerator MoveTowardsTarget(GameObject target, NavMeshAgent navMeshAgent)
        {
            float rotationSpeed = 5;

            // Keep moving until the NPC is within stopping distance of the target
            while (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                // Stop if the target was deactivated during movement
                if (!target.activeInHierarchy)
                {
                    ConvaiLogger.DebugLog("Target deactivated during movement.", ConvaiLogger.LogCategory.Actions);
                    yield break;
                }

                // Wait a frame if the agent is not moving yet
                if (navMeshAgent.velocity.sqrMagnitude < Mathf.Epsilon) yield return null;

                // Smoothly rotate towards the movement direction
                RotateTowardsMovementDirection(navMeshAgent, rotationSpeed);
                yield return null;
            }
        }

        /// <summary>
        ///     Smoothly rotates the NPC towards its current movement direction.
        /// </summary>
        private void RotateTowardsMovementDirection(NavMeshAgent navMeshAgent, float rotationSpeed)
        {
            Quaternion rotation = Quaternion.LookRotation(navMeshAgent.velocity.normalized);
            // Lock X and Z axis to only rotate on the horizontal plane
            rotation.x = 0;
            rotation.z = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
        }

        /// <summary>
        ///     Called when the NPC finishes moving. Resets animation and re-enables root motion.
        /// </summary>
        private void FinishMovement(Animator animator, GameObject target)
        {
            // Return to idle animation
            animator.CrossFade(Animator.StringToHash("Idle"), 0.1f);

            // Rotate towards the camera only if this is the only action
            if (_actions.Count == 1 && Camera.main != null) StartCoroutine(RotateTowardsCamera());

            // Re-enable root motion after movement
            animator.applyRootMotion = true;

            // Notify listeners that the MoveTo action has ended
            ActionEnded?.Invoke("MoveTo", target);
        }

        /// <summary>
        ///     Coroutine that smoothly rotates the NPC to face the main camera after moving.
        /// </summary>
        private IEnumerator RotateTowardsCamera()
        {
            if (Camera.main != null)
            {
                // Calculate the direction from the NPC to the camera
                Vector3 direction = (Camera.main.transform.position - transform.position).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                float elapsedTime = 0f;
                float rotationTime = 2f;

                // Smoothly rotate over 2 seconds
                while (elapsedTime < rotationTime)
                {
                    // Lock rotation to the Y axis only
                    targetRotation.x = 0;
                    targetRotation.z = 0;
                    transform.rotation =
                        Quaternion.Slerp(transform.rotation, targetRotation, elapsedTime / rotationTime);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
            }
        }


        /// <summary>
        ///     Coroutine to pick up a target GameObject, adjusting the NPCs' rotation and playing animations.
        /// </summary>
        /// <param name="target">The target GameObject to pick up.</param>
        private IEnumerator PickUp(GameObject target)
        {
            // Invoke the ActionStarted event with the "PickUp" action and the target GameObject.
            ActionStarted?.Invoke("PickUp", target);

            // Check if the target GameObject is null. If it is, log an error and exit the coroutine.
            if (target == null)
            {
                ConvaiLogger.DebugLog("Target is null! Exiting PickUp coroutine.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            // Check if the target GameObject is active. If not, log an error and exit the coroutine.
            if (!target.activeInHierarchy)
            {
                ConvaiLogger.DebugLog($"Target: {target.name} is inactive! Exiting PickUp coroutine.",
                    ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            // Calculate the direction from the NPC to the target, ignoring the vertical (y) component.
            Vector3 direction = (target.transform.position - transform.position).normalized;
            direction.y = 0;

            // Calculate the target rotation to face the target direction.
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float elapsedTime = 0f;
            float rotationTime = 0.5f;

            // Smoothly rotate the NPC towards the target direction over a specified time.
            while (elapsedTime < rotationTime)
            {
                targetRotation.x = 0;
                targetRotation.z = 0;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsedTime / rotationTime);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Log the action of picking up the target along with its name.
            ConvaiLogger.DebugLog($"Picking up Target: {target.name}", ConvaiLogger.LogCategory.Actions);

            // Retrieve the Animator component from the current NPC.
            Animator animator = _currentNPC.GetComponent<Animator>();

            // Start the "Picking Up" animation with a cross-fade transition.
            animator.CrossFade(Animator.StringToHash("Picking Up"), 0.1f);

            // Wait for one second to ensure that the Animator has had time to transition to the "Picking Up" animation state.
            yield return new WaitForSeconds(1);

            // Define the time it takes for the hand to reach the object in the "Picking Up" animation.
            // This is a specific point in time during the animation that we are interested in.
            float timeToReachObject = 1f;

            // Wait for the time it takes for the hand to reach the object.
            yield return new WaitForSeconds(timeToReachObject);

            // Check again if the target is still active before attempting to pick it up.
            if (!target.activeInHierarchy)
            {
                ConvaiLogger.DebugLog(
                    $"Target: {target.name} became inactive during the pick up animation! Exiting PickUp coroutine.",
                    ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            // Once the hand has reached the object, set the target's parent to the NPCs' transform,
            // effectively "picking up" the object, and then deactivate the object.
            target.transform.parent = gameObject.transform;
            target.SetActive(false);

            // Transition back to the "Idle" animation.
            animator.CrossFade(Animator.StringToHash("Idle"), 0.4f);

            // Invoke the ActionEnded event with the "PickUp" action and the target GameObject.
            ActionEnded?.Invoke("PickUp", target);
        }

        /// <summary>
        ///     Drops a previously picked up target GameObject back into the world.
        /// </summary>
        /// <param name="target">The target GameObject to drop.</param>
        private void Drop(GameObject target)
        {
            // Notify listeners that the Drop action has started
            ActionStarted?.Invoke("Drop", target);

            // Check if the target is null before proceeding
            if (target == null)
            {
                ConvaiLogger.DebugLog("Target is null! Exiting Drop function.", ConvaiLogger.LogCategory.Actions);
                return;
            }

            ConvaiLogger.DebugLog($"Dropping Target: {target.name}", ConvaiLogger.LogCategory.Actions);

            // Detach the object from the NPC, place it at the NPC's feet, and reactivate it
            target.transform.parent = null;
            target.transform.position = transform.position;
            target.SetActive(true);

            // Notify listeners that the Drop action has ended
            ActionEnded?.Invoke("Drop", target);
        }

        private IEnumerator Jump()
        {
            ActionStarted?.Invoke("Jump", _currentNPC.gameObject);

            Animator animator = _currentNPC.GetComponent<Animator>();
            Rigidbody rb = GetComponent<Rigidbody>();

            // Appliquer la force une seule fois
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); // reset Y
            rb.AddForce(new Vector3(0f, 5f, 0f), ForceMode.Impulse);

            animator.CrossFadeInFixedTime(Animator.StringToHash("Jumping"), 0.1f);

            // Attendre que l'animation démarre
            yield return new WaitForSeconds(0.11f);

            // Récupérer la durée réelle du clip comme AnimationActions et Crouch
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            float length = (clipInfo != null && clipInfo.Length > 0) ? clipInfo[0].clip.length : 1.5f;

            yield return new WaitForSeconds(length);

            animator.CrossFadeInFixedTime(Animator.StringToHash("Idle"), 0.1f);

            yield return null;
            ActionEnded?.Invoke("Jump", _currentNPC.gameObject); // ← à la fin comme Crouch
        }

        /// <summary>
        ///     Coroutine to throw a target GameObject in the NPC's forward direction.
        ///     Plays a throwing animation, detaches the object, and applies physics forces.
        /// </summary>
        /// <param name="target">The target GameObject to throw. Must have a Rigidbody.</param>
        private IEnumerator Throw(GameObject target)
        {
            // Check that the target exists
            if (target == null)
            {
                ConvaiLogger.DebugLog("Throw target is null!", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            // Check that the target has a Rigidbody to apply physics forces
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null)
            {
                ConvaiLogger.DebugLog($"{target.name} has no Rigidbody!", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            ActionStarted?.Invoke("Throw", target);

            Animator animator = _currentNPC.GetComponent<Animator>();

            // Play the throwing animation
            animator.CrossFadeInFixedTime(Animator.StringToHash("Throwing"), 0.05f);

            // Wait for the Animator to transition into the throwing state
            yield return new WaitForSeconds(0.11f);

            // Get the actual length of the throwing animation clip
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            float animLength = (clipInfo != null && clipInfo.Length > 0) ? clipInfo[0].clip.length : 1.5f;

            // Wait until halfway through the animation before releasing the object
            yield return new WaitForSeconds(animLength * 0.5f);

            // Detach the object from the NPC and reposition it slightly in front
            target.transform.parent = null;
            target.transform.position += new Vector3(0.5f, 1.0f, 0.5f);
            target.transform.rotation = Quaternion.identity;
            target.SetActive(true);

            // Enable physics on the thrown object
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Apply a force combining the NPC's forward and upward direction
            Vector3 throwDirection = (_currentNPC.transform.up + _currentNPC.transform.forward).normalized;
            rb.AddForce(throwDirection * 10f, ForceMode.VelocityChange);

            // Wait for the second half of the animation to finish
            yield return new WaitForSeconds(animLength * 0.5f);

            // Return to idle animation with a smooth transition
            animator.CrossFadeInFixedTime(Animator.StringToHash("Idle"), 0.1f);

            ActionEnded?.Invoke("Throw", target);
        }

        /// <summary>
        ///     Coroutine to make the NPC vault over an obstacle and move to the target.
        ///     Activates NavMesh links temporarily to allow passage through barriers.
        /// </summary>
        /// <param name="target">The target GameObject to move to after vaulting.</param>
        private IEnumerator PassThroughAndMoveTo(GameObject target)
        {
            // Notify listeners that the PassThrough action has started
            ActionStarted?.Invoke("PassThrough", _currentNPC.gameObject);

            Animator animator = _currentNPC.GetComponent<Animator>();
            VaultingConvai vault = _currentNPC.GetComponent<VaultingConvai>();

            // Signal the vaulting system to begin
            vault.toVault = true;

            // Activate all NavMesh links in the scene to allow crossing barriers
            NavMeshLink[] navLinks = FindObjectsOfType<NavMeshLink>();
            foreach (var link in navLinks) link.enabled = true;

            // Wait briefly for the NavMesh to recalculate with the active links
            yield return new WaitForSeconds(0.5f);

            // Move to the target through the now-accessible link
            yield return MoveTo(target);

            // Wait briefly after crossing
            yield return new WaitForSeconds(0.3f);

            // Stop vaulting
            vault.toVault = false;

            // Disable all NavMesh links again to restore normal pathfinding
            foreach (var link in navLinks) link.enabled = false;

            // Return to idle animation
            animator.CrossFade(Animator.StringToHash("Idle"), 0.1f);

            // Notify listeners that the PassThrough action has ended
            ActionEnded?.Invoke("PassThrough", _currentNPC.gameObject);
        }


        public class MatchTargetParameters
        {
            public Vector3 matchPos;
            public Vector3 matchPosWeight;
            public AvatarTarget matchBodyPart;
            public float matchStartTime;
            public float matchTargetTime;
        }

    /// <summary>
        ///     Applies root motion matching to align the NPC's body part with a target position during an animation.
        /// </summary>
        private void MatchTarget(global::MatchTargetParameters matchTargetParams)
        {
            Animator m_animator = _currentNPC.GetComponent<Animator>();

            // Do not apply match target if already matching or in a transition
            if (m_animator.isMatchingTarget || m_animator.IsInTransition(0)) return;

            m_animator.MatchTarget(
                matchTargetParams.matchPos,
                transform.rotation,
                matchTargetParams.matchBodyPart,
                new MatchTargetWeightMask(matchTargetParams.matchPosWeight, 0.0f),
                matchTargetParams.matchStartTime,
                matchTargetParams.matchTargetTime
            );
        }

       /// <summary>
        ///     Coroutine that plays a vaulting animation, optionally rotating and matching a target position.
        /// </summary>
        public IEnumerator PerformVaulting(string animName, global::MatchTargetParameters matchTargetParameters = null,
            Quaternion targetRotation = new Quaternion(), bool shouldRotate = false, bool mirrored = false)
        {
            float rotationSpeed = 5f;
            Animator m_animator = _currentNPC.GetComponent<Animator>();
            Rigidbody m_rigidBody = _currentNPC.GetComponent<Rigidbody>();

            // Mark the NPC as vaulting and freeze its velocity
            IsVaulting = true;
            m_rigidBody.linearVelocity = Vector3.zero;
            m_rigidBody.angularVelocity = Vector3.zero;

            // Enable the vaulting state in the Animator and play the animation
            m_animator.SetBool("IsVaulting", true);
            m_animator.CrossFadeInFixedTime(animName, 0.2f);

            // Wait for the transition into the vaulting animation to start
            float timeout = 3f;
            float timer = 0f;
            yield return new WaitUntil(() =>
            {
                timer += Time.deltaTime;
                return m_animator.IsInTransition(0) || timer >= timeout;
            });

            // Wait for the transition to finish
            timer = 0f;
            yield return new WaitUntil(() =>
            {
                timer += Time.deltaTime;
                return !m_animator.IsInTransition(0) || timer >= timeout;
            });

            // Get the length of the vault animation (85% to avoid overshooting the end)
            var animState = m_animator.GetCurrentAnimatorStateInfo(0);
            float animLength = animState.length > 0 ? animState.length * 0.85f : 1f;

            float rotateStartTime = (matchTargetParameters != null) ? matchTargetParameters.matchStartTime : 0f;
            float time = 0.0f;

            // Play the vault animation frame by frame, applying rotation and IK matching as needed
            while (time <= animLength)
            {
                time += Time.deltaTime;
                float normalizedTime = time / animLength;

                // Rotate towards the target rotation if required
                if (shouldRotate && normalizedTime > rotateStartTime)
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                // Apply match target to align the NPC's body with the vault obstacle
                if (matchTargetParameters != null && normalizedTime <= matchTargetParameters.matchTargetTime)
                    MatchTarget(matchTargetParameters);

                yield return null;
            }

            // End the vaulting state
            m_animator.SetBool("IsVaulting", false);
            IsVaulting = false;
            Debug.Log("Vault finished");
        }

    

        /// <summary>
        ///     Coroutine to make the NPC walk to a chair and sit down.
        ///     Blocks PlayActionList until StandUp is called.
        /// </summary>
        /// <param name="target">The chair GameObject. Must have a Chair component with a sitPoint assigned.</param>
        private IEnumerator Sit(GameObject target)
        {
            // Notify listeners that the Sit action has started
            ActionStarted?.Invoke("Sit", _currentNPC.gameObject);

            NavMeshAgent navMeshAgent = _currentNPC.GetComponent<NavMeshAgent>();
            Animator m_animator = _currentNPC.GetComponent<Animator>();

            // Validate target and chair component
            if (target == null) { Debug.LogError("Sit: target is null"); yield break; }
            if (!target.TryGetComponent<Chair>(out Chair chair)) { Debug.LogError("Sit: no Chair script on target"); yield break; }
            if (chair.sitPoint == null) { Debug.LogError("Sit: sitPoint not assigned on chair"); yield break; }
            if (chair.isOccupied) yield break;

            // Set the NavMeshAgent destination to the sit point
            navMeshAgent.SetDestination(chair.sitPoint.position);

            // Wait for the NPC to arrive, with a 10-second timeout as a safety net
            float timeout = 10f;
            float timer = 0f;
            while (navMeshAgent.remainingDistance > 0.5f)
            {
                timer += Time.deltaTime;
                if (timer >= timeout) break;
                yield return null;
            }

            // Wait two extra frames to let FinishMovement complete fully
            yield return null;
            yield return null;

            // Stop the NavMeshAgent and snap the NPC to the exact sit position and rotation
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
            transform.position = chair.sitPoint.position;
            transform.rotation = chair.sitPoint.rotation;

            // Disable root motion so the animation does not move the NPC away from the chair
            m_animator.applyRootMotion = false;

            // Trigger the sitting animation via the Animator parameter
            m_animator.SetBool("IsSitting", true);

            // Disable head tracking body rotation while sitting
            if (TryGetComponent<ConvaiHeadTracking>(out var headTracking))
                headTracking.SetActionRunning(true);

            // Mark the chair as occupied
            //chair.isOccupied = true;

            // Mark the NPC as sitting in code
            isSitting = true;
            
            // Store the sit position and rotation to lock the NPC in place each frame
            Vector3 sitPosition = chair.sitPoint.position;
            Quaternion sitRotation = chair.sitPoint.rotation;

            // Start the position lock coroutine to prevent physics or animation from moving the NPC
            StartCoroutine(LockPosition(sitPosition, sitRotation));


            // Notify listeners that the Sit action has ended
            ActionEnded?.Invoke("Sit", _currentNPC.gameObject);
        }
        
        /// <summary>
        ///     Coroutine to make the NPC stand up from a chair and return to idle.
        /// </summary>
        private IEnumerator StandUp()
        {
            // Notify listeners that the StandUp action has started
            ActionStarted?.Invoke("StandUp", _currentNPC.gameObject);

            Debug.Log($"StandUp called, isSitting = {isSitting}");

            // Only proceed if the NPC is currently sitting
            if (!isSitting) { Debug.LogWarning("StandUp: not sitting, ignoring"); yield break; }

            Animator m_animator = _currentNPC.GetComponent<Animator>();
            NavMeshAgent navMeshAgent = _currentNPC.GetComponent<NavMeshAgent>();

            // Unblock the WaitUntil in Sit()
            isSitting = false;

            // Trigger the stand up animation via the Animator parameter
            m_animator.SetBool("IsSitting", false);

            // Re-enable the NavMeshAgent and place it correctly on the NavMesh
            navMeshAgent.isStopped = false;
            navMeshAgent.Warp(transform.position);
            m_animator.applyRootMotion = true;

            // Return to idle animation
            m_animator.CrossFadeInFixedTime(Animator.StringToHash("Idle"), 0.1f);

            // Wait briefly then confirm the agent is back on the NavMesh before allowing new actions
            yield return new WaitForSeconds(0.2f);
            yield return new WaitUntil(() => navMeshAgent.isOnNavMesh);

            // Re-enable head tracking
            if (TryGetComponent<ConvaiHeadTracking>(out var headTracking))
                headTracking.SetActionRunning(false);

            Debug.Log("StandUp: standing!");

            // Notify listeners that the StandUp action has ended
            ActionEnded?.Invoke("StandUp", _currentNPC.gameObject);
        }
        /// <summary>
        ///     Coroutine that locks the NPC's position and rotation every frame while sitting,
        ///     preventing physics, root motion or other forces from moving the character.
        /// </summary>
        /// <param name="position">The world position to lock to.</param>
        /// <param name="rotation">The world rotation to lock to.</param>
        private IEnumerator LockPosition(Vector3 position, Quaternion rotation)
        {
            // Hold the NPC at the sit position every frame until they stand up
            while (isSitting)
            {
                transform.position = position;
                transform.rotation = rotation;
                yield return null;
            }

            // Once no longer sitting, warp the NavMeshAgent to the current position
            // to ensure it is correctly placed on the NavMesh
            NavMeshAgent agent = _currentNPC.GetComponent<NavMeshAgent>();
            if (agent != null) agent.Warp(transform.position);
        }
    #endregion
    }
}