﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using Torch.Managers.PatchManager;
using VRageMath;

namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class MergeBlockPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static readonly HashSet<long> MergeBlockCache = new HashSet<long>();

        private static DateTime _lastLogTime;



        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyShipMergeBlock).GetMethod("CheckUnobstructed", BindingFlags.NonPublic | BindingFlags.Instance )).
                Prefixes.Add(typeof(MergeBlockPatch).GetMethod(nameof(MergeCheck), BindingFlags.NonPublic | BindingFlags.Static));

            ctx.GetPattern(typeof(MyShipMergeBlock).GetMethod("AddConstraint",  BindingFlags.NonPublic|BindingFlags.Instance )).
                Suffixes.Add(typeof(MergeBlockPatch).GetMethod(nameof(AddBlocks), BindingFlags.NonPublic | BindingFlags.Static));

        }

        private static bool MergeCheck(MyShipMergeBlock __instance)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits  || !BlockLimiterConfig.Instance.MergerBlocking) return true;

            var mergeBlock = __instance;

            if (mergeBlock?.Other == null || MergeBlockCache.Contains(mergeBlock.CubeGrid.EntityId) || MergeBlockCache.Contains(mergeBlock.Other.CubeGrid.EntityId)
                || !mergeBlock.Enabled || !mergeBlock.Other.Enabled || mergeBlock.IsLocked || !mergeBlock.IsFunctional || !mergeBlock.Other.IsFunctional || mergeBlock.CubeGrid == mergeBlock.Other.CubeGrid)

            {
                return true;
            }



            if (Grid.CanMerge(mergeBlock.CubeGrid, mergeBlock.Other.CubeGrid, out var blocks, out var count, out var limitName))
            {
                if (!MergeBlockCache.Contains(mergeBlock.CubeGrid.EntityId))
                {
                    MergeBlockCache.Add(mergeBlock.CubeGrid.EntityId);
                }
  
                return true;
            }

            mergeBlock.Enabled = false;

            if (DateTime.Now - _lastLogTime < TimeSpan.FromSeconds(1)) return false;
            _lastLogTime = DateTime.Now;
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,blocks,limitName,count);
            var remoteUserId = MySession.Static.Players.TryGetSteamId(mergeBlock.OwnerId);
            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(mergeBlock.OwnerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Utilities.SendFailSound(remoteUserId);

            BlockLimiter.Instance.Log.Info($"Blocked merger between {mergeBlock.CubeGrid?.DisplayName} and {mergeBlock.Other?.CubeGrid?.DisplayName}");
            return false;

        }

        private static void AddBlocks(MyShipMergeBlock __instance)
        {
            var id = __instance.CubeGrid.EntityId;

            Task.Run((() =>
            {
                Thread.Sleep(10000);
                if (!GridCache.TryGetGridById(id, out var grid))
                {
                    return;
                }

                UpdateLimits.GridLimit(grid);

            }));
        }
    }
}