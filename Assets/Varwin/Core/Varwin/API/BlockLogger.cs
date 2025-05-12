using System;
using System.Collections.Generic;
using System.Linq;
using Core.Varwin;
using UnityEngine;
using Varwin.Data;

namespace Varwin
{
    public class BlocklyLogger : MonoBehaviour
    {
        private static BlocklyLogger Instance;
        private static readonly Dictionary<string, IBlocklyLogger> ActiveLoggers = new Dictionary<string, IBlocklyLogger>();
        private static readonly Dictionary<string, IBlocklyLogger> FinishedLoggers = new Dictionary<string, IBlocklyLogger>();

        private const float RepeatRate = 1f;

        public static void RefreshLogic()
        {
            foreach (var logger in ActiveLoggers.Values)
            {
                logger.ClearBlocks();
                FinishedLoggers.Add(logger.Id, logger);
            }

            ActiveLoggers.Clear();
        }

        public static IBlocklyLogger EnterScope(string id)
        {
            if (!Instance)
            {
                Instance = new GameObject("[Blockly Logger]").AddComponent<BlocklyLogger>();
            }

            if (LoaderAdapter.LoaderType != typeof(ApiLoader))
            {
                return new BlocklyLoggerDummy(id);
            }

            IBlocklyLogger logger;

            if (FinishedLoggers.ContainsKey(id))
            {
                logger = FinishedLoggers[id];
                FinishedLoggers.Remove(id);
                logger.ClearBlocks();
            }
            else
            {
                if (ActiveLoggers.ContainsKey(id))
                {
                    logger = ActiveLoggers[id];
                    logger.ClearBlocks();
                }
                else
                {
                    logger = new BlocklyLoggerDesktop(id);    
                }
            }

            if (!ActiveLoggers.ContainsKey(id))
            {
                ActiveLoggers.Add(id, logger);
            }

            return logger;
        }

        public static void ExitScope(IBlocklyLogger logger)
        {
            ActiveLoggers.Remove(logger.Id);

            if (!FinishedLoggers.ContainsKey(logger.Id))
            {
                FinishedLoggers.Add(logger.Id, logger);
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            OnGameModeChanged(ProjectData.GameMode);
            ProjectData.GameModeChanged += OnGameModeChanged;
        }

        private void OnGameModeChanged(GameMode newMode)
        {
            if (newMode == GameMode.Preview)
            {
                InvokeRepeating(nameof(SendLog), 0, RepeatRate);
            }
            else
            {
                CancelInvoke();
            }
        }

        private void SendLog()
        {
            object command = new
            {
                command = PipeCommandType.RunningBlocks,
                blocks = GetBlocksToSend(),
                sceneId = ProjectData.SceneId
            };

            if (CommandPipe.Instance)
            {
                CommandPipe.Instance.SendPipeCommand(command);
            }

            FinishedLoggers.Clear();
        }

        private string[] GetBlocksToSend()
        {
            var blocks = new HashSet<string>();

            foreach (IBlocklyLogger logger in ActiveLoggers.Values)
            {
                try
                {
                    blocks.AddRange(logger.GetBlocks());
                }
                catch (ArgumentNullException)
                {
                }
            }

            foreach (IBlocklyLogger logger in FinishedLoggers.Values)
            {
                try
                {
                    blocks.AddRange(logger.GetBlocks());
                }
                catch (ArgumentNullException)
                {
                }
            }

            return blocks.ToArray();
        }
    }

    public class BlocklyLoggerDesktop : IBlocklyLogger
    {
        private readonly HashSet<string> _blocks = new HashSet<string>();

        public string Id { get; set; }

        public BlocklyLoggerDesktop(string id)
        {
            Id = id;
        }

        public void Log(string block)
        {
            _blocks.Add(block);
        }

        public HashSet<string> GetBlocks() => _blocks;

        public void ClearBlocks()
        {
            _blocks.Clear();
        }
    }


    public class BlocklyLoggerDummy : IBlocklyLogger
    {
        public string Id { get; set; }

        public void Log(string block)
        {
        }

        public HashSet<string> GetBlocks() => null;

        public void ClearBlocks()
        {
        }

        public BlocklyLoggerDummy(string id)
        {
            Id = id;
        }
    }


    public interface IBlocklyLogger
    {
        string Id { get; set; }
        void Log(string block);
        HashSet<string> GetBlocks();
        void ClearBlocks();
    }
}
