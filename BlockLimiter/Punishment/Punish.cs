using System;
using System.Collections.Generic;
using System.Linq;
using BlockLimiter.Patch;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using VRage.Collections;

namespace BlockLimiter.Punishment
{
    public class Punish : ProcessHandlerBase
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly HashSet<MySlimBlock> _blockCache = new HashSet<MySlimBlock>();
        private static bool _firstCheckCompleted;

        public override int GetUpdateResolution()
        {
            return Math.Max(BlockLimiterConfig.Instance.PunishInterval,1) * 1000;
        }

        public override void Handle()
        {

            if (!BlockLimiterConfig.Instance.EnableLimits)return;
            GridCache.GetBlocks(_blockCache);
            RunPunishment(_blockCache);
            _blockCache.Clear();
        }

        public static int RunPunishment(HashSet<MySlimBlock> blocks,List<LimitItem.PunishmentType>punishmentTypes = null)
        {
            
            var totalBlocksPunished = 0;

            if (blocks.Count == 0 || !BlockLimiterConfig.Instance.EnableLimits)
            {
                return 0;
            }

            var limitItems = BlockLimiterConfig.Instance.AllLimits.Where(item => item.FoundEntities.Count > 0 && item.Punishment != LimitItem.PunishmentType.None).ToList();

            if (limitItems.Count == 0) return 0;

            var punishBlocks = new MyConcurrentDictionary<MySlimBlock,LimitItem.PunishmentType>();

            for (var i = limitItems.Count - 1; i >= 0; i--)
            {
                var item = limitItems[i];
                if (punishmentTypes != null && !punishmentTypes.Contains(item.Punishment)) continue;
                var idsToRemove = new HashSet<long>();
                var punishCount = 0;
                foreach (var (id, count) in item.FoundEntities)
                {
                    if (id == 0 || item.IsExcepted(id))
                    {
                        idsToRemove.Add(id);
                        continue;
                    }

                    if (count <= item.Limit) continue;


                    foreach (var block in blocks)
                    {
                        if (block.CubeGrid.IsPreview || !item.IsMatch(block.BlockDefinition))
                        {
                            continue;
                        }


                        var defBase = MyDefinitionManager.Static.GetDefinition(block.BlockDefinition.Id);

                        if (defBase != null && !_firstCheckCompleted && !defBase.Context.IsBaseGame) continue;

                        if (Math.Abs(punishCount - count) <= item.Limit)
                        {
                            break;
                        }

                        if (item.IgnoreNpcs)
                        {
                            if (MySession.Static.Players.IdentityIsNpc(block.FatBlock.BuiltBy) ||
                                MySession.Static.Players.IdentityIsNpc(block.FatBlock.OwnerId))

                            {
                                idsToRemove.Add(id);
                                continue;
                            }
                        }

                        //Reverting to old shutoff due to performance issues
                        if (item.Punishment == LimitItem.PunishmentType.ShutOffBlock &&
                            block.FatBlock is MyFunctionalBlock fBlock && (!fBlock.Enabled ||
                                                                           block.FatBlock.MarkedForClose ||
                                                                           block.FatBlock.Closed))
                        {
                            punishCount++;
                            continue;
                        }

                        //Todo Fix this function and re-implement. Currently too expensive
                        /*
                        if (item.Punishment == LimitItem.PunishmentType.ShutOffBlock && Math.Abs(GetDisabledBlocks(id,item) - count) <= item.Limit )
                        {
                            continue;
                        }
                        */
                        var playerSteamId = MySession.Static.Players.TryGetSteamId(id);

                        if (playerSteamId > 0 && !Annoy.AnnoyQueue.ContainsKey(playerSteamId))
                        {
                            Annoy.AnnoyQueue[playerSteamId] = DateTime.Now;
                            break;

                        }

                        if (item.LimitGrids && block.CubeGrid.EntityId == id)
                        {
                            punishCount++;
                            punishBlocks[block] = item.Punishment;
                            continue;
                        }

                        if (item.LimitPlayers)
                        {
                            if (Block.IsOwner(block, id))
                            {
                                punishCount++;
                                punishBlocks[block] = item.Punishment;
                                continue;
                            }
                        }

                        if (!item.LimitFaction) continue;
                        var faction = MySession.Static.Factions.TryGetFactionById(id);
                        if (faction == null || block.FatBlock.GetOwnerFactionTag()?.Equals(faction.Tag) == false)
                            continue;
                        punishCount++;
                        punishBlocks[block] = item.Punishment;
                    }
                }
            }

            totalBlocksPunished = punishBlocks.Count;
            _firstCheckCompleted = !_firstCheckCompleted;
            if (totalBlocksPunished == 0)
            {
                return totalBlocksPunished;
            }
            Log.Debug($"Punishing {punishBlocks.Count} blocks");
            Block.Punish(punishBlocks);

            /*
            List<MySlimBlock> GetDisabledBlocks(long id, LimitItem limit)
            {
                var disabledBlocks = new List<MySlimBlock>();
                foreach (var block in blocks)
                {
                    if (!(block.FatBlock is MyFunctionalBlock fBlock) || block.FatBlock.MarkedForClose || block.FatBlock.Closed) continue;
                    if (block.CubeGrid.EntityId != id && !Block.IsOwner(block,id)) continue;
                    if (BlockSwitchPatch.KeepOffBlocks.Contains(block.FatBlock))
                    {
                        disabledBlocks.Add(block);
                        continue;
                    }
                    if (fBlock.Enabled)continue;
                    disabledBlocks.Add(block);
                }

                return disabledBlocks;
            }

            int GetDisabledCount (long id, LimitItem limit)
            {
                var disabledCount = 0;
                foreach (var block in blocks)
                {
                    if (!limit.IsGridType(block.CubeGrid)) continue;
                    if (!limit.IsMatch(block.BlockDefinition)) continue;
                    if (!(block.FatBlock is MyFunctionalBlock fBlock) || block.FatBlock.MarkedForClose || block.FatBlock.Closed) continue;
                    if (block.CubeGrid.EntityId != id && !Block.IsOwner(block,id)) continue;
                    if (BlockSwitchPatch.KeepOffBlocks.Contains(block.FatBlock))
                    {
                        disabledCount++;
                        continue;
                    }
                    if (fBlock.Enabled)continue;
                    disabledCount++;
                }
                return disabledCount;
            }
            */
            return totalBlocksPunished;

        }

    }
}