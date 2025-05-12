using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using Varwin.Public;

namespace Varwin
{
    public static class LogicUtils
    {
#if !NET_STANDARD_2_0
        private static readonly Dictionary<string, DelegateData> RegisteredDelegatesData = new(); 

        public static void AddEventHandler(this object logic, object target, string eventName, string methodName)
        {
            SetEventHandler(EventHandlerAction.Add, logic, target, eventName, methodName);
        }

        [Obsolete]
        public static void AddEventHandler(this ILogic logic, object target, string eventName, string methodName)
        {
            SetEventHandler(EventHandlerAction.Add, logic, target, eventName, methodName);
        }
        
        public static void RemoveEventHandler(this object logic, object target, string eventName, string methodName)
        {
            SetEventHandler(EventHandlerAction.Remove, logic, target, eventName, methodName);
        }
        
        [Obsolete]
        public static void RemoveEventHandler(this ILogic logic, object target, string eventName, string methodName)
        {
            SetEventHandler(EventHandlerAction.Remove, logic, target, eventName, methodName);
        }

        private static void SetEventHandler(EventHandlerAction action, object logic, object target, string eventName, string methodName)
        {
            if (target is DynamicCollection<dynamic> behaviourColl)
            {
                foreach (var behaviour in behaviourColl.Collection)
                {
                    var wrapper = (behaviour as MonoBehaviour)?.gameObject.GetWrapper();
                    SetEventHandler(action, logic, behaviour, eventName, methodName, wrapper?.GetName() + wrapper?.GetInstanceId());
                }
            }
            else if (target is DynamicCollection<Wrapper> wrappers)
            {
                foreach (var wrapper in wrappers.Collection)
                {
                    SetEventHandler(action, logic, wrapper, eventName, methodName, wrapper?.GetName() + wrapper?.GetInstanceId());
                }
            }
            else
            {
                SetEventHandler(action, logic, target, eventName, methodName, "alone");
            }
        }
        
        public static void RemoveAllEventHandlers()
        {
            foreach (var registeredDelegateData in RegisteredDelegatesData)
            {
                DelegateData data = registeredDelegateData.Value;
                data.EventInfo.RemoveEventHandler(data.Target, registeredDelegateData.Value.Delegate);
            }
            
            RegisteredDelegatesData.Clear();
            
            GCManager.Collect();
        }

        private static void SetEventHandler(EventHandlerAction eventHandlerAction, object logic, object target, string eventName, string methodName, string wrapperName)
        {
            string prefix = eventHandlerAction == EventHandlerAction.Add ? "AddEventHandler" : "RemoveEventHandler";
            
            // Get Target Object EventInfo
            
            if (target == null)
            {
                var message = $"{prefix}: Target object can't be null (Event: \"{eventName}\"; Method: \"{methodName}\")";
                throw new Exception(message);
            }
            
            Type targetType = target.GetType();
            EventInfo eventInfo = targetType.GetEvent(eventName);
            
            if (eventInfo == null)
            {
                var message = $"{prefix}: Not found event with name \"{eventName}\" (TargetType: \"{targetType.FullName}\"; Method: \"{methodName}\")";
                throw new Exception(message);
            }

            // Get Logic MethodInfo
            
            if (logic == null)
            {
                var message = $"{prefix}: Logic instance can't be null (Event: \"{eventName}\"; Method: \"{methodName}\")";
                throw new Exception(message);
            }
            
            Type logicType = logic.GetType();
            MethodInfo methodInfo = logicType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (methodInfo == null)
            {
                var message = $"{prefix}: Not found method with name \"{methodName}\" (TargetType: \"{targetType.FullName}\"; Event: \"{eventName}\")";
                throw new Exception(message);
            }
            
            // Get Delegate Method

            var delegateKey = $"{target}__{wrapperName}_{eventName}__{methodName}";
            Delegate delegatedMethod = CreateDelegate(logic, target, eventInfo, methodInfo);

            if (delegatedMethod == null)
            {
                var message = $"{prefix}: Can't create delegate (TargetType: \"{targetType.FullName}\"; Event: \"{eventName}\"; EventHandlerType: \"{eventInfo.EventHandlerType}\"; Method: \"{methodName}\")";
                throw new Exception(message);
            }

            // Add or Remove EventHandler
            
            if (eventHandlerAction == EventHandlerAction.Add)
            {
                eventInfo.AddEventHandler(target, delegatedMethod);
                RegisteredDelegatesData.Add(delegateKey, new DelegateData
                {
                    Target = target,
                    EventInfo = eventInfo,
                    Delegate = delegatedMethod
                });
            }
            else
            {
                eventInfo.RemoveEventHandler(target, delegatedMethod);
                RegisteredDelegatesData.Remove(delegateKey);
            }
        }

        private static Delegate CreateDelegate(object logic, object target, EventInfo eventInfo, MethodInfo methodInfo)
        {
            var eventTypes = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters().Select(param => param.ParameterType);
            var methodTypes = methodInfo.GetParameters().Select(param => param.ParameterType);
           
            if (eventTypes.Count() == methodTypes.Count())
            {
                return Delegate.CreateDelegate(eventInfo.EventHandlerType, logic, methodInfo);
            }
     
            var paramList = eventTypes.Select(Expression.Parameter).ToList();
            
            var logicConstant = Expression.Constant(logic);

            if (target is MonoBehaviour monoBehaviour)
            {
                target = monoBehaviour.gameObject.GetWrapper();
            }
            
            var objectConstant = Expression.Constant(target);

            var methodTypesArray = methodTypes.ToArray();
            var argsArray =  paramList.Concat(new List<Expression> {objectConstant}).ToArray();

            for (int i = 0; i < methodTypesArray.Length; i++)
            {
                if (methodTypesArray[i] != argsArray[i].Type && argsArray[i].Type.IsSubclassOf(methodTypesArray[i]))
                {
                    argsArray[i] = Expression.Convert(argsArray[i], methodTypesArray[i]);
                }
            }

            BlockExpression blockExpr; 
                
            if (methodInfo.ReturnType == typeof(IEnumerator))
            {
                var startCor = logic.GetType().GetMethod("StartCoroutine", new[] {typeof(IEnumerator)});
                blockExpr = Expression.Block(
                    paramList,
                    Expression.RuntimeVariables(paramList),
                    Expression.Call(logicConstant, startCor, Expression.Call(logicConstant, methodInfo, argsArray))
                );
            }
            else
            {
                blockExpr = Expression.Block(
                    paramList,
                    Expression.RuntimeVariables(paramList),
                    Expression.Call(logicConstant, methodInfo, paramList.Concat(new List<Expression> {objectConstant}))
                );
            }
            
            return Expression.Lambda(eventInfo.EventHandlerType, blockExpr, paramList).Compile();
        }

        private enum EventHandlerAction
        {
            Add,
            Remove
        }

        private struct DelegateData
        {
            public EventInfo EventInfo;
            public object Target;
            public Delegate Delegate;
        }
#else
        public static void RemoveAllEventHandlers()
        {
            
        }
#endif
    }
}