﻿using System.Collections;
using UnityEngine;
using Varwin.SocketLibrary.Extension;

namespace Varwin.SocketLibrary
{
    /// <summary>
    /// Часть контроллера, отвечающая за анимации.
    /// </summary>
    public partial class SocketController
    {
        /// <summary>
        /// Соединение точек с анимацией с учетом кривой времени.
        /// </summary>
        /// <param name="plugPoint">Вилка.</param>
        /// <param name="socketPoint">Розетка.</param>
        /// <param name="curve">Кривая.</param>
        /// <param name="animationTime">Время анимации.</param>
        public void ConnectWithAnimation(PlugPoint plugPoint, SocketPoint socketPoint, AnimationCurve curve, float animationTime = 10f)
        {
            if (!JointPoints.Contains(plugPoint) && !JointPoints.Contains(socketPoint))
            {
                return;
            }

            StartCoroutine(AnimateConnection(plugPoint, socketPoint, animationTime, curve));
        }

        /// <summary>
        /// Соединение с анимацией.
        /// </summary>
        /// <param name="plugPoint">Вилка.</param>
        /// <param name="socketPoint">Розетка.</param>
        /// <param name="animationTime">Длительность анимации.</param>
        public void ConnectWithAnimation(PlugPoint plugPoint, SocketPoint socketPoint, float animationTime = 10f)
        {
            if (!JointPoints.Contains(plugPoint) && !JointPoints.Contains(socketPoint))
            {
                return;
            }

            StartCoroutine(AnimateConnection(plugPoint, socketPoint, animationTime, AnimationCurve.Linear(0, 0, 1, 1)));
        }

        /// <summary>
        /// Процесс анимации.
        /// </summary>
        /// <param name="plugPoint">Вилка.</param>
        /// <param name="socketPoint">Розетка.</param>
        /// <param name="animTime">Время анимации.</param>
        /// <param name="curve">Кривая анимации.</param>
        public static IEnumerator AnimateConnection(PlugPoint plugPoint, SocketPoint socketPoint, float animTime, AnimationCurve curve)
        {
            var time = 0f;

            while (time < animTime && Vector3.Distance(plugPoint.transform.position, socketPoint.transform.position) > 0.0001f)
            {
                plugPoint.SocketController.Rigidbody.velocity = Vector3.zero;
                plugPoint.SocketController.Rigidbody.angularVelocity = Vector3.zero;
                var timeCoef = time / animTime;
                
                plugPoint.SocketController.gameObject.TransformToSocket(socketPoint, plugPoint, curve.Evaluate(Mathf.Pow(timeCoef, 4f)));
                time += Time.deltaTime;
                yield return null;
            }

            Connect(plugPoint, socketPoint);
        }
    }
}