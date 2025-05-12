using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Varwin.Core.Behaviours;
using Varwin.Multiplayer.NetworkBehaviours.v2;
using Varwin.Public;
using Varwin.SocketLibrary;
using JointPoint = Varwin.SocketLibrary.JointPoint;

namespace Varwin.Multiplayer
{
    public static class MultiplayerHelper
    {
        public const int MaxChildCountObjects = 1000;
        private static FieldInfo _latestParentInfo;

        public static void AddSyncComponents(GameObject targetObject)
        {
            targetObject.gameObject.AddNetworkComponent<NetworkObjectController>();
            targetObject.transform.ForEachObjectInHierarchy(AddInteractableComponentsToTransform);
            AddComponents(targetObject);
        }

        private static void AddComponents(GameObject targetObject)
        {
            var components = targetObject.GetComponentsInChildren<Component>(true);
            foreach (var component in components)
            {
                if (component is Collider collider)
                {
                    var networkCollider = collider.gameObject.AddNetworkComponent<NetworkCollider>();
                    networkCollider.Initialize(collider);
                }

                if (component is Rigidbody rigidbody)
                {
                    var networkRigidbody = rigidbody.gameObject.AddNetworkComponent<NetworkRigidbody>();
                    if (!component.GetComponent<NetworkInteractableObject>() && component.GetComponent<NetworkTransform>())
                    {
                        rigidbody.gameObject.AddNetworkComponent<NetworkTransform>();
                    }
                    
                    networkRigidbody.Initialize(rigidbody);
                }

                if (component is ObjectPointerBehaviour objectPointerBehaviour)
                {
                    var networkObjectPointer = objectPointerBehaviour.gameObject.AddNetworkComponent<NetworkObjectPointerBehaviour>();
                    networkObjectPointer.Initialize(objectPointerBehaviour);
                }

                if (component is Animation animation)
                {
                    var networkAnimation = animation.gameObject.AddComponent<NetworkAnimation>();
                    networkAnimation.Initialize(animation);
                }

                if (component is VarwinBot varwinBot)
                {
                    varwinBot.gameObject.AddComponent<NetworkVarwinBot>();
                }

                if (component is VarwinBotTextBubble varwinBotTextBubble)
                {
                    varwinBotTextBubble.gameObject.AddComponent<NetworkVarwinBotTextBubble>();
                }
                
                if (component is VarwinBotTextToSpeech varwinBotTextToSpeech)
                {
                    varwinBotTextToSpeech.gameObject.AddComponent<NetworkVarwinBotTextToSpeech>();
                }

                if (component is PlugPoint plugPoint)
                {
                    if (plugPoint && plugPoint.SocketController)
                    {
                        plugPoint.SocketController.gameObject.AddComponent<NetworkSocketController>();
                    }

                    plugPoint.gameObject.AddNetworkComponent<NetworkPlugPoint>();
                }
                
                if (component is SocketPoint socketPoint)
                {
                    if (socketPoint && socketPoint.SocketController)
                    {
                        socketPoint.SocketController.gameObject.AddComponent<NetworkSocketController>();
                    }

                    socketPoint.gameObject.AddNetworkComponent<NetworkSocketPoint>();
                }

                if (component is InteractableObjectBehaviour interactableObjectBehaviour)
                {
                    interactableObjectBehaviour.gameObject.AddComponent<NetworkInteractableObjectBehaviour>();
                }
            }
        }

        private static void AddInteractableComponentsToTransform(Transform transform)
        {
            if (transform.GetComponent<NetworkInteractableObject>() || transform.GetComponent<NetworkTransform>())
            {
                return;
            }
            
            var grabStartAwareExist = transform.GetComponent<IVarwinInputAware>() != null;
            var touchStartAwareExist = transform.GetComponent<IVarwinInputAware>() != null;
            var useStartAwareExist = transform.GetComponent<IVarwinInputAware>() != null;
            
            if (grabStartAwareExist || touchStartAwareExist || useStartAwareExist)
            {
                transform.gameObject.AddNetworkComponent<NetworkInteractableObject>();
            }
            else
            {
                if (transform.GetComponent<Rigidbody>())
                {
                    transform.gameObject.AddNetworkComponent<NetworkTransform>();
                }
            }
        }

        public static void AddNetworkBehaviours(GameObject varwinObject)
        {
            var behaviours = varwinObject.GetComponents<VarwinBehaviour>().Where(x => !BehavioursCollection.IsAnOldTypeEnumBehaviour(x.GetType()));
            foreach (var varwinBehaviour in behaviours)
            {
                if (NetworkBehaviourHelper.TryGetCorrespondingBehaviourType(varwinBehaviour, out var networkBehaviourType))
                {
                    var varwinNetworkBehaviour = varwinObject.gameObject.AddComponent(networkBehaviourType) as VarwinNetworkBehaviour;
                    varwinNetworkBehaviour.InitializeNetworkBehaviour(varwinBehaviour);
                }
            }
        }

        public static void AddSyncComponentsToVarwinObjects()
        {
            var varwinObjects = GameObject.FindObjectsOfType<VarwinObject>(true);
            foreach (var varwinObject in varwinObjects)
            {
                AddSyncComponents(varwinObject.gameObject);
                AddNetworkBehaviours(varwinObject.gameObject);
            }
        }

        private static uint GetId(GameObject targetGameObject)
        {
            if (!targetGameObject.transform.parent)
            {
                return (uint) (targetGameObject.GetWrapper().GetInstanceId() * MaxChildCountObjects);
            }
            
            var sumOffset = 0;
            var linearHierarchy = GetLinearHierarchy(targetGameObject.transform.root);
            sumOffset += linearHierarchy.IndexOf(targetGameObject.transform) + 1;
            return (uint) (targetGameObject.GetWrapper().GetInstanceId() * MaxChildCountObjects + sumOffset);    
        }

        private static List<Transform> GetLinearHierarchy(Transform targetTransform)
        {
            var result = new List<Transform>();
            result.Add(targetTransform);
            foreach (Transform transform in targetTransform)
            {
                result.AddRange(GetLinearHierarchy(transform));
            }

            return result;
        }

        private static void SetupNetworkObject(GameObject gameObject)
        {
            if (!gameObject)
            {
                return;
            }

            var currentTransform = gameObject.transform;
            while (currentTransform)
            {
                var networkObject = currentTransform.gameObject.GetComponent<NetworkObject>();
                if (!networkObject)
                {
                    networkObject = currentTransform.gameObject.AddComponent<NetworkObject>();
                }
                
                networkObject.IsSceneObject = true;
                uint id = GetId(currentTransform.gameObject);
                networkObject.GlobalObjectIdHash = id;
                networkObject.NetworkObjectId = id;
                if (currentTransform.parent)
                {
                    networkObject.TrySetParent(currentTransform.parent);
                }
                
                currentTransform = currentTransform.parent;
            }
        }
        
        private static T AddNetworkComponent<T>(this GameObject targetGameObject) where T: Component
        {
            SetupNetworkObject(targetGameObject);
            return targetGameObject.AddComponent<T>();
        }
        
        private static void ForEachObjectInHierarchy(this Transform targetTransform, Action<Transform> callBack)
        {
            callBack?.Invoke(targetTransform);
            foreach (Transform child in targetTransform)
            {
                child.ForEachObjectInHierarchy(callBack);
            }
        }

    }
}