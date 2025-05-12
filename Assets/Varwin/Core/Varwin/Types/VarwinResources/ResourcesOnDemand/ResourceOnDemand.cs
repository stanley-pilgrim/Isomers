using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varwin.Data.ServerData;

namespace Varwin
{
    public abstract class ResourceOnDemand
    {
        public abstract ResourceDto DTO
        {
            get;
            set;
        }
    }
    public abstract class ResourceOnDemand<T> : ResourceOnDemand where T: class
    {
        protected ResourceDto _dto;
        
        public override ResourceDto DTO
        {
            get => _dto;
            set
            {
                if (_dto == value)
                {
                    return;
                }

                if (value == null)
                {
                    Resource = null;
                    OnUnloaded?.Invoke();
                }
                else
                {
                    Resource = (T) GameStateData.GetResourceValue(value.Guid);
                    
                    if (IsResourceExist)
                    {
                        OnLoaded?.Invoke(Resource);
                    }
                    else
                    {
                        OnUnloaded?.Invoke();
                    }
                }
                
                _dto = value;
            }
        }
        
        public T Resource { get; protected set; }

        public event Action<T> OnLoaded;
        public event Action OnUnloaded;

        public ResourceOnDemand()
        {
            LoaderAdapter.Loader.ResourceLoaded += OnResourceLoaded;
            LoaderAdapter.Loader.ResourceUnloaded += OnResourceUnloaded;
            
            ProjectData.GameModeChanging += OnGameModeChanging;
        }

        public ResourceOnDemand(ResourceDto dto) : this()
        {
            DTO = dto;
        }

        ~ResourceOnDemand()
        {
            if (IsResourceExist)
            {
                DestroyResource();
            }
            
            LoaderAdapter.Loader.ResourceLoaded -= OnResourceLoaded;
            LoaderAdapter.Loader.ResourceUnloaded -= OnResourceUnloaded;
            
            ProjectData.GameModeChanging -= OnGameModeChanging;
        }

        private void OnGameModeChanging(GameMode newGameMode)
        {
            Unload();
        }

        public void Load()
        {
            if (DTO == null)
            {
                return;
            }
            
            DTO.ForceLoad = true;
            LoaderAdapter.Loader.LoadResource(DTO);
        }

        public virtual void Unload()
        {
            if (DTO == null)
            {
                return;
            }
            
            DTO.ForceLoad = false;
            LoaderAdapter.Loader.LoadResource(DTO);
        }

        protected virtual void OnResourceLoaded(ResourceDto dto, object resourceValue)
        {
            if (DTO == null)
            {
                return;
            }

            if (dto != DTO)
            {
                return;
            }
            
            Resource = (T) resourceValue;
            OnLoaded?.Invoke(Resource);
        }

        private void OnResourceUnloaded(ResourceDto dto)
        {
            if (DTO == null)
            {
                return;
            }
            
            if (dto != DTO)
            {
                return;
            }

            if (IsResourceExist)
            {
                DestroyResource();
            }
            
            OnUnloaded?.Invoke();
        }

        protected abstract bool IsResourceExist { get; }
        protected abstract void DestroyResource();

        protected void OnLoadedCall(T resource) => OnLoaded?.Invoke(resource);
        protected void OnUnloadedCall() => OnUnloaded?.Invoke();
        
        public static implicit operator T(ResourceOnDemand<T> t) => t?.Resource;
        public static implicit operator bool(ResourceOnDemand<T> t) => t != null;
    }
}