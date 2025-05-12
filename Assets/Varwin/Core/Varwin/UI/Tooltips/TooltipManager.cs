using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Varwin;
using Object = UnityEngine.Object;
using ObjectTooltipSize = Varwin.UI.ObjectTooltip.ObjectTooltipSize;
using ObjectTooltipDirection = Varwin.UI.ObjectTooltip.ObjectTooltipDirection;

namespace Varwin.UI
{
    public static class TooltipManager
    {
        private static ControllerTooltipManager _controllerTooltipManager;

        public static bool ControllersTooltipsInitialized;
        
        private static readonly Dictionary<ControllerTooltipManager.TooltipControllers, bool> _toolTipsOnControllers = new Dictionary<ControllerTooltipManager.TooltipControllers, bool>();
        
        public static void InitializeControllerTooltips()
        {
            if (!_controllerTooltipManager)
            {
                _controllerTooltipManager = Object.FindObjectOfType<ControllerTooltipManager>();
            }

            ControllersTooltipsInitialized = _controllerTooltipManager;
            
            if (!_toolTipsOnControllers.ContainsKey(ControllerTooltipManager.TooltipControllers.Left))
            {
                _toolTipsOnControllers.Add(ControllerTooltipManager.TooltipControllers.Left, false);
            }

            if (!_toolTipsOnControllers.ContainsKey(ControllerTooltipManager.TooltipControllers.Right))
            {
                _toolTipsOnControllers.Add(ControllerTooltipManager.TooltipControllers.Right, false);
            }
        }
        
        public static bool ShowControllerTooltip(string text, ControllerTooltipManager.TooltipControllers controller, ControllerTooltipManager.TooltipButtons button, bool buttonGlow = true)
        {
            InitializeControllerTooltips();

            if (!ControllersTooltipsInitialized)
            {
                return false;
            }

            if (controller == ControllerTooltipManager.TooltipControllers.Both)
            {
                _controllerTooltipManager.SetTooltip(text, ControllerTooltipManager.TooltipControllers.Left, button, buttonGlow);
                _controllerTooltipManager.SetTooltip(text, ControllerTooltipManager.TooltipControllers.Right, button, buttonGlow);
                _toolTipsOnControllers[ControllerTooltipManager.TooltipControllers.Left] = true;
                _toolTipsOnControllers[ControllerTooltipManager.TooltipControllers.Left] = true;
            }
            else
            {
                _controllerTooltipManager.SetTooltip(text, controller, button, buttonGlow);
                _toolTipsOnControllers[controller] = true;
            }

            return true;
        }

        public static bool CheckShowingToolTip(ControllerTooltipManager.TooltipControllers controller) => _toolTipsOnControllers.ContainsKey(controller) && _toolTipsOnControllers[controller];

        public static bool HideControllerTooltip(ControllerTooltipManager.TooltipControllers controller, ControllerTooltipManager.TooltipButtons button)
        {
            InitializeControllerTooltips();

            if (!ControllersTooltipsInitialized)
            {
                return false;
            }

            if (controller == ControllerTooltipManager.TooltipControllers.Both)
            {
                _controllerTooltipManager.HideTooltip(ControllerTooltipManager.TooltipControllers.Left, button);
                _controllerTooltipManager.HideTooltip(ControllerTooltipManager.TooltipControllers.Right, button);
                _toolTipsOnControllers[ControllerTooltipManager.TooltipControllers.Left] = false;
                _toolTipsOnControllers[ControllerTooltipManager.TooltipControllers.Left] = false;
            }
            else
            {
                _controllerTooltipManager.HideTooltip(controller, button);
                _toolTipsOnControllers[controller] = false;
            }

            return true;
        }

        public static bool TriggerHapticPulse(ControllerTooltipManager.TooltipControllers controller,
            float strength = 0.05f,
            float duration = 0.1f,
            float interval = 0.005f)
        {
            InitializeControllerTooltips();

            if (!ControllersTooltipsInitialized)
            {
                return false;
            }

            if (controller == ControllerTooltipManager.TooltipControllers.Both)
            {
                _controllerTooltipManager.Vibrate(ControllerTooltipManager.TooltipControllers.Left, strength, duration, interval);
                _controllerTooltipManager.Vibrate(ControllerTooltipManager.TooltipControllers.Right, strength, duration, interval);
            }
            else
            {
                _controllerTooltipManager.Vibrate(controller, strength, duration, interval);
            }

            return true;
        }
        
        public static void SetObjectTooltip(GameObject tooltippedObject, string tooltipText)
        {
            ObjectTooltipManager.SetObjectTooltip(tooltippedObject, tooltipText, ObjectTooltipSize.Large, 0.3f, ObjectTooltipDirection.Up);       
        }
        
        public static void SetObjectTooltip(GameObject tooltippedObject, string tooltipText, ObjectTooltipSize tooltipSize)
        {
            ObjectTooltipManager.SetObjectTooltip(tooltippedObject, tooltipText, tooltipSize, 0.3f, ObjectTooltipDirection.Up);
        }
        
        public static void SetObjectTooltip(GameObject tooltippedObject, string tooltipText, ObjectTooltipSize tooltipSize, float verticalOffset)
        {
            ObjectTooltipManager.SetObjectTooltip(tooltippedObject, tooltipText, tooltipSize, verticalOffset, ObjectTooltipDirection.Up);       
        }
        
        public static void SetObjectTooltip(GameObject tooltippedObject, string tooltipText, ObjectTooltipSize tooltipSize, float verticalOffset, ObjectTooltipDirection tooltipDirection)
        {
            ObjectTooltipManager.SetObjectTooltip(tooltippedObject, tooltipText, tooltipSize, verticalOffset, tooltipDirection);
        }

        public static void RemoveObjectTooltip(GameObject tooltippedObject)
        {
            ObjectTooltipManager.RemoveObjectTooltip(tooltippedObject);
        }

        public static void UpdateObjectElements(GameObject tooltippedObject)
        {
            ObjectTooltipManager.UpdateObjectElements(tooltippedObject);
        }
    }
}