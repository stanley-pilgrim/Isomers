﻿using System;
using System.Collections;
using System.Collections.Generic;
using Entitas;
using Varwin.Models.Data;
using UnityEngine;
using Varwin.Data;
using Varwin.Data.ServerData;

namespace Varwin.ECS.Systems
{
    public sealed class SpawnAssetSystem : IExecuteSystem, ICleanupSystem
    {
        private readonly IGroup<GameEntity> _prefabEntities;
        private readonly IGroup<GameEntity> _spawnEntities;
        private bool _haveJoints;
        private bool _internalSpawn;
        private static Dictionary<int, JointData> _joints;


        public SpawnAssetSystem(Contexts contexts)
        {
            _spawnEntities = contexts.game.GetGroup(GameMatcher.SpawnAsset);
            _prefabEntities = contexts.game.GetGroup(GameMatcher
                .AllOf(GameMatcher.ServerObject)
            );
        }

        public void Execute()
        {
            if (!ProjectData.ObjectsAreLoaded)
            {
                return;
            }

            _internalSpawn = false;
            var somethingSpawned = false;
            
            _joints = new Dictionary<int, JointData>();
            foreach (var spawnEntity in _spawnEntities.GetEntities())
            {
                foreach (var prefabEntity in _prefabEntities.GetEntities())
                {
                    if (!spawnEntity.hasSpawnAsset)
                    {
                        continue;
                    }

                    if (!prefabEntity.hasServerObject)
                    {
                        continue;
                    }

                    if (prefabEntity.serverObject.Value.Id != spawnEntity.spawnAsset.SpawnInitParams.IdObject)
                    {
                        continue;
                    }

                    try
                    {
                        somethingSpawned = true;
                        SpawnObject(prefabEntity, spawnEntity);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Spawn error at object \"{spawnEntity.spawnAsset.SpawnInitParams.Name}\" [{spawnEntity.spawnAsset.SpawnInitParams.IdServer}]!\n{e}");
                        spawnEntity.Destroy();
                    }
                }
            }

            if (_haveJoints)
            {
                _haveJoints = false;
                ProjectData.Joints = _joints;
                ProjectDataListener.Instance.RestoreJoints(_joints);
            }

            if (!somethingSpawned)
            {
                return;
            }

            if (_internalSpawn)
            {
                ProjectData.OnObjectInitialSpawnProcessCompleted();
            }
            else
            {
                ProjectData.OnObjectSpawnProcessCompleted();
            }
        }

        public void Cleanup()
        {
            if (!ProjectData.ObjectsAreLoaded)
            {
                return;
            }

            foreach (var entity in _spawnEntities.GetEntities())
            {
                EcsUtils.Destroy(entity);
            }
        }

        #region METHODS

        private void SpawnObject(GameEntity prefabEntity, GameEntity spawnEntity)
        {
            var spawnedGameObject = UnityEngine.Object.Instantiate(prefabEntity.gameObject.Value);
            spawnedGameObject.name = spawnedGameObject.name.Replace("(Clone)", "");
            
            var joint = spawnEntity.spawnAsset.SpawnInitParams.Joints;
            _internalSpawn = spawnEntity.spawnAsset.SpawnInitParams.InternalSpawn;
            
            Helper.InitObject(prefabEntity.serverObject.Value.Id, spawnEntity.spawnAsset.SpawnInitParams, spawnedGameObject, prefabEntity.serverObject.Value.Name, _internalSpawn);

            int instanceId = spawnEntity.spawnAsset.SpawnInitParams.IdInstance;

            if (instanceId == 0 || joint == null)
            {
                return;
            }

            _haveJoints = true;
            _joints.Add(instanceId, joint);
        }

        #endregion
    }
}


