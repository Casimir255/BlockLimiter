using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlockLimiter.Patch;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace BlockLimiter.Utility
{
    public static class Block
    {
        public static bool AllowBlock(MyCubeBlockDefinition block, long playerId = 0, long gridId = 0)
        {
            var allow = true;
            var blockCache = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blockCache);

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            GridCache.TryGetGridById(gridId, out var grid);


            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                
                if (!item.BlockList.Any() || !IsMatch(block, item)) continue;
                
               
                if (item.Exceptions.Any())
                {
                    if (Utilities.IsExcepted(gridId, item.Exceptions)) continue;
                    if (Utilities.IsExcepted(playerId, item.Exceptions)) continue;
                }

                if (grid != null) 
                {
                    if (!Grid.IsGridType(grid,item)) continue;
                    if (item.Limit == 0) return false;

                    if (item.LimitGrids)
                    {
                        var subGrids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);

                        if (!item.FoundEntities.TryGetValue(gridId, out var gCount))
                        {
                            var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid == grid && IsMatch(x.BlockDefinition,item) && IsOwner(x, playerId));
                            if (filteredBlocksCount >= item.Limit)
                            {
                                allow = false;
                                break;
                            }

                        }
                        
                        if (gCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }

                        if (subGrids.Any())
                        {
                            foreach (var subGrid in subGrids)
                            {
                                var subGridBlockCount = blockCache.Count(x=> x.CubeGrid == subGrid && IsMatch(x.BlockDefinition,item) && IsOwner(x, playerId));
                                if (subGridBlockCount < item.Limit) continue;
                                return false;

                            }
                        }
                    }
                }

                if (item.Limit == 0) return false;
                if (playerId > 0 && item.LimitPlayers)
                {
                    if (!item.FoundEntities.TryGetValue(playerId, out var pCount))
                    {
                        var filteredBlocksCount = blockCache.Count(x=> IsMatch(x.BlockDefinition,item) && IsOwner(x, playerId));
                        if (filteredBlocksCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }
                    }

                    if (pCount >= item.Limit)
                    {
                        allow = false;
                        break;
                    }
                }


                if (faction == null || !item.LimitFaction) continue;
                {
                    if (!item.FoundEntities.TryGetValue(faction.FactionId, out var fCount))
                    {
                        var filteredBlocksCount = blockCache.Count(x =>
                            IsMatch(x.BlockDefinition,item) &&  x.FatBlock.GetOwnerFactionTag() == faction.Tag);
                        if (filteredBlocksCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }
                    }

                    if (fCount < item.Limit) continue;
                    allow = false;
                    break;
                }

            }

            return allow;
            
        }
        
        public static bool AllowBlock(MyCubeBlockDefinition block, long playerId, MyObjectBuilder_CubeGrid grid = null)
        {
            
            var allow = true;
            var blockCache = new HashSet<MySlimBlock>();
            GridCache.GetBlocks(blockCache);

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                if (!item.BlockList.Any() || !IsMatch(block, item)) continue;
                
                if (grid != null && faction != null && (Utilities.IsExcepted(playerId,item.Exceptions) || Utilities.IsExcepted(faction.FactionId,item.Exceptions) || Utilities.IsExcepted(grid.EntityId,item.Exceptions)))
                    continue;

                if (grid != null)
                {
                    var gridId = grid.EntityId;
                    if (!Grid.IsGridType(grid, item)) continue;
                    if (item.Limit == 0) return false;

                    if (gridId > 0 && item.LimitGrids)
                    {

                        if (!item.FoundEntities.TryGetValue(gridId, out var gCount))
                        {
                            var filteredBlocksCount = blockCache.Count(x=> x.CubeGrid.EntityId == grid.EntityId && IsMatch(x.BlockDefinition,item) && IsOwner(x, playerId));
                            if (filteredBlocksCount >= item.Limit)
                            {
                                allow = false;
                                break;
                            }

                        }
                        
                        if (gCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }

                    

                    }
                }


                if (item.Limit == 0) return false;
                if (playerId > 0 && item.LimitPlayers)
                {
                    if (!item.FoundEntities.TryGetValue(playerId, out var pCount))
                    {
                        var filteredBlocksCount = blockCache.Count(x=> IsMatch(x.BlockDefinition,item) && IsOwner(x, playerId));
                        if (filteredBlocksCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }
                    }

                    if (pCount >= item.Limit)
                    {
                        allow = false;
                        break;
                    }
                }



                if (faction == null || !item.LimitFaction) continue;
                {
                    if (!item.FoundEntities.TryGetValue(faction.FactionId, out var fCount))
                    {
                        var filteredBlocksCount = blockCache.Count(x =>
                            IsMatch(x.BlockDefinition,item) &&  x.FatBlock.GetOwnerFactionTag() == faction.Tag);
                        if (filteredBlocksCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }
                    }

                    if (fCount < item.Limit) continue;
                    allow = false;
                    break;
                }
                
                
            }

            return allow;
            
        }
        

        public static bool IsOwner(MySlimBlock block, long playerId)
        {
            return block.BuiltBy == playerId || block.OwnerId == playerId;
        }

        public static bool IsMatch(MyCubeBlockDefinition block, LimitItem item)
        {
            if (!item.BlockList.Any()) return false;
            return item.BlockList.Any(x => x.Equals(block.Id.SubtypeId.ToString(), StringComparison.OrdinalIgnoreCase))
                   || item.BlockList.Any(x => x.Equals(block.Id.TypeId.ToString().Substring(16), StringComparison.OrdinalIgnoreCase))
                   || item.BlockList.Any(x=>x.Equals(block.BlockPairName,StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryAdd(MySlimBlock block, MyCubeGrid grid)
        {
            return block != null && TryAdd(block.BlockDefinition, block.BuiltBy, grid.EntityId);
        }


        public static bool TryAdd(MyCubeBlockDefinition block, long playerId, long gridId = 0)
        {
            if (block == null) return false;

            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(limit => IsMatch(block, limit)))
            {
                if (playerId > 0)
                {
                    if (limit.LimitFaction && playerFaction != null)
                        limit.FoundEntities.AddOrUpdate(playerFaction.FactionId, 1, (l, i) => i + 1);
                    if (limit.LimitPlayers)
                        limit.FoundEntities.AddOrUpdate(playerId, 1, (l, i) => i + 1);
                }

                if (!limit.LimitGrids || gridId <= 0) continue;
                if (!GridCache.TryGetGridById(gridId, out var grid) || !Grid.IsGridType(grid, limit)) continue;
                limit.FoundEntities.AddOrUpdate(gridId, 1, (l, i) => i + 1);

            }
            return true;


        }
        public static bool TryRemove(MyCubeBlockDefinition block, long playerId, long gridId = 0)
        {
            if (block == null) return false;
            
            var playerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);



            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(limit => IsMatch(block, limit)))
            {
                if (playerId > 0)
                {
                    if (limit.LimitFaction && playerFaction != null)
                        limit.FoundEntities.AddOrUpdate(playerFaction.FactionId, 0, (l, i) => Math.Max(0,i - 1));
                    if (limit.LimitPlayers)
                        limit.FoundEntities.AddOrUpdate(playerId, 0, (l, i) => Math.Max(0,i - 1));
                }

                if (!limit.LimitGrids || gridId <= 0) continue;
                if (!GridCache.TryGetGridById(gridId, out var grid) || !Grid.IsGridType(grid, limit)) continue;
                limit.FoundEntities.AddOrUpdate(gridId, 0, (l, i) => Math.Max(0,i - 1));

            }
            return true;


        }

        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {
            var newList = new List<MySlimBlock>();
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                nonAllowedBlocks = newList;
                return true;
            }
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                limit.FoundEntities.TryGetValue(id, out var currentCount);
                if(Utilities.IsExcepted(id, limit.Exceptions)) continue;
                var affectedBlocks = blocks.Where(x => IsMatch(x.BlockDefinition, limit)).ToList();
                if (affectedBlocks.Count <= limit.Limit - currentCount ) continue;
                var take = affectedBlocks.Count - (limit.Limit - currentCount);
                newList.AddRange(affectedBlocks.Where(x=>!newList.Contains(x)).Take(take));
            }

            nonAllowedBlocks = newList;
            return newList.Count == 0;
        }


        public static void RemoveBlock(MySlimBlock block)
        {
            var blockDef = block?.FatBlock?.BlockDefinition;
            
            if (blockDef == null) return;
            
            var blockOwner = block.OwnerId;
            var blockBuilder = block.BuiltBy;
            var blockGrid = block.CubeGrid.EntityId;
            var faction = MySession.Static.Factions.TryGetFactionByTag(block.FatBlock.GetOwnerFactionTag())?.FactionId;
            
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits.Where(x => IsMatch(blockDef, x)))
            {
                if (limit.LimitGrids && blockGrid > 0)
                {
                    limit.FoundEntities.AddOrUpdate(blockGrid, 0, (l, i) => Math.Max(0,i - 1));
                }

                if (limit.LimitPlayers)
                {
                    if (blockOwner > 0 && blockOwner == blockBuilder && IsOwner(block, blockOwner))
                        limit.FoundEntities.AddOrUpdate(blockOwner, 0, (l, i) => Math.Max(0,i - 1));
                    else
                    {
                        if (blockOwner > 0 && IsOwner(block, blockOwner))
                            limit.FoundEntities.AddOrUpdate(blockOwner, 0, (l, i) => Math.Max(0,i - 1));
                        if (blockBuilder > 0 && IsOwner(block, blockBuilder))
                            limit.FoundEntities.AddOrUpdate(blockBuilder, 0, (l, i) => Math.Max(0,i - 1));
                    }
                }

                if (limit.LimitFaction && faction != null)
                {
                    limit.FoundEntities.AddOrUpdate((long)faction, 0, (l, i) => Math.Max(0,i - 1));
                }
            }

        }


    }
}