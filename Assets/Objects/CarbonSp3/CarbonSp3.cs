using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Varwin;
using Varwin.Public;
using Varwin.SocketLibrary;
using System;

namespace Varwin.Types.CarbonSp3_fda4979f83ec485e885b236072a59935
{
    [VarwinComponent(English: "Carbon Sp3", Russian: "Углерод Sp3")]
    public class CarbonSp3 : VarwinObject
    {
        private SocketController _socketController;

        private PlugPoint[] _plugPoints;
        private SocketPoint[] _socketPoints;
        private List<VarwinObject> _connectedAtoms = new List<VarwinObject>();

        private string atomLetters;
        private int valence;

        private void Start()
        {
            valence = 4;
            atomLetters = "";

            // собираем все вилки и розетки
            _plugPoints = GetComponentsInChildren<PlugPoint>();
            _socketPoints = GetComponentsInChildren<SocketPoint>();

            // ищем SocketController
            var plugPoints = GetComponentsInChildren<PlugPoint>();
            if (plugPoints.Length == 0) return;

            _socketController = plugPoints[0].SocketController;

            // подписываемся на соединение и разъединение
            _socketController.OnConnect += HandleConnect;
            _socketController.OnDisconnect += HandleDisconnect;

            // проверяем, есть ли соединения
            CheckExistingConnections();
        }

        private void CheckExistingConnections()
        {
            // проверяем вилки
            foreach (var plug in _plugPoints)
            {
                if (!plug.IsFree && plug.ConnectedPoint != null)
                {
                    // находим и блокируем парную розетку
                    DisableMatchingSocket(plug);

                    // добавляем в список подключённый атом
                    var connectedObj = plug.ConnectedPoint.transform.root.gameObject;
                    var connectedAtom = connectedObj.GetComponent<VarwinObject>();
                    if (connectedAtom != null && !_connectedAtoms.Contains(connectedAtom))
                    {
                        _connectedAtoms.Add(connectedAtom);
                    }

                    // к вилке может подключаться только другой углерод
                    atomLetters += "c";
                }
            }

            // проверяем розетки
            foreach (var socket in _socketPoints)
            {
                if (!socket.IsFree && socket.ConnectedPoint != null)
                {
                    // находим и блокируем парную розетку
                    DisableMatchingPlug(socket);

                    // добавляем в список подключённый атом
                    var connectedObj = socket.ConnectedPoint.transform.root.gameObject;
                    var connectedAtom = connectedObj.GetComponent<VarwinObject>();
                    if (connectedAtom != null && !_connectedAtoms.Contains(connectedAtom))
                    {
                        _connectedAtoms.Add(connectedAtom);
                    }

                    // получаем ключ и добавляем к сигнатуре
                    PlugPoint connectedPlug = socket.ConnectedPoint as PlugPoint;
                    var key = connectedPlug.Key;
                    atomLetters += key;
                }
            }
        }

        private void DisableMatchingSocket(PlugPoint plug)
        {
            // находим родителя-связь
            Transform parent = plug.transform.parent;
            // ищем розетку с тем же родителем и выключаем
            foreach (var socket in _socketPoints)
            {
                if (socket.transform.parent == parent)
                {
                    socket.CanConnect = false;
                    break;
                }
            }
        }

        private void DisableMatchingPlug(SocketPoint socket)
        {
            // находим родителя-связь
            Transform parent = socket.transform.parent;
            // ищем вилку с тем же родителем и выключаем
            foreach (var plug in _plugPoints)
            {
                if (plug.transform.parent == parent)
                {
                    plug.CanConnect = false;
                    break;
                }
            }
        }

        private void EnableMatchingSocket(PlugPoint plug)
        {
            // ищем розетку с тем же родителем
            Transform parent = plug.transform.parent;
            foreach (var socket in _socketPoints)
            {
                if (socket.transform.parent == parent)
                {
                    socket.CanConnect = true;
                    break;
                }
            }
        }

        private void EnableMatchingPlug(SocketPoint socket)
        {
            // ищем вилку с тем же родителем
            Transform parent = socket.transform.parent;
            foreach (var plug in _plugPoints)
            {
                if (plug.transform.parent == parent)
                {
                    plug.CanConnect = true;
                    break;
                }
            }
        }

        // добавить атом в список подключённых
        private void AddConnectedAtom(GameObject connectedObj)
        {
            var atom = connectedObj.GetComponent<VarwinObject>();
            if (atom != null && !_connectedAtoms.Contains(atom))
            {
                _connectedAtoms.Add(atom);
            }
        }

        // удалить атом из списка подключённых
        private void RemoveConnectedAtom(GameObject connectedObj)
        {
            var atom = connectedObj.GetComponent<VarwinObject>();
            if (atom != null)
            {
                _connectedAtoms.Remove(atom);
            }
        }

        // удалить букву из сигнатуры
        private void RemoveLetter(char letter)
        {
            int index = atomLetters.IndexOf(letter);
            if (index >= 0)
            {
                atomLetters = atomLetters.Remove(index, 1);
            }
        }

        // обработка подключения
        private void HandleConnect(SocketPoint socketPoint, PlugPoint plugPoint)
        {
            var key = plugPoint.Key;
            
            // если это углерод и мы подключились в его розетку
            if (key[0] == 'c' && _plugPoints.Contains(plugPoint))
            {
                DisableMatchingSocket(plugPoint);
                AddConnectedAtom(socketPoint.transform.root.gameObject);
                atomLetters += "c";
            }

            // иначе если подключились в нашу розетку
            else if (_socketPoints.Contains(socketPoint))
            {
                DisableMatchingPlug(socketPoint);
                AddConnectedAtom(plugPoint.transform.root.gameObject);
                atomLetters += key[0];
            }  
        }

        // обработка отключения
        private void HandleDisconnect(SocketPoint socketPoint, PlugPoint plugPoint)
        {
            var key = plugPoint.Key;

            // если мы отключаемся от углерода
            if (key[0] == 'c' && _plugPoints.Contains(plugPoint))
            {
                EnableMatchingSocket(plugPoint);
                RemoveConnectedAtom(socketPoint.transform.root.gameObject);
                RemoveLetter('c');
            }

            // иначе если это от нас отключились
            else if (_socketPoints.Contains(socketPoint))
            {
                EnableMatchingPlug(socketPoint);
                RemoveConnectedAtom(plugPoint.transform.root.gameObject);
                RemoveLetter(key[0]);
            }
        }

        // создание сигнатуры
        public string MakeSignature()
        {
            string signature = new string(atomLetters.OrderBy(x => x).ToArray());
            signature = string.Concat("c-", signature);
            return signature;
        }
    }
}
