using System;
using System.Collections.Generic;
using System.Linq;
using BlockLimiter.PluginApi;
using BlockLimiter.Settings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using VRage.Game;
using VRage.Groups;

namespace BlockLimiter.Utility
{
    public static class Grid
    {

        public static bool IsOwner(MyCubeGrid grid, long id)
        {
            if (grid == null || id == 0) return false;
            return GridCache.GetOwners(grid).Contains(id) || GridCache.GetBuilders(grid).Contains(id);
        }

        public static bool IsSizeViolation(long id)
        {
            return GridCache.TryGetGridById(id, out var grid) && IsSizeViolation(grid,false, out _);
        }

        public static bool IsSizeViolation(MyObjectBuilder_CubeGrid grid)
        {
            if (Utilities.IsExcepted(grid)) return false;

            if (grid == null)
            {
                return false;
            }
            var gridSize = grid.CubeBlocks.Count;
            var gridType = grid.GridSizeEnum;
            var isStatic = grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && gridSize >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                return  true;
            }
            

            return false;
        }

        private static bool IsSizeViolation(MyCubeGrid grid, bool converting, out int count)
        {
            count = 0;
            if (grid == null) return false;

            if (Utilities.IsExcepted(grid))
                return false;

            var gridSize = grid.CubeBlocks.Count;
            var gridType = grid.GridSizeEnum;
            var isStatic = converting? !grid.IsStatic:grid.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlockSizeShips);
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlockSizeStations);
                return  true;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlocksLargeGrid);
                return  true;
            }
            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid <= 0 || gridType != MyCubeSize.Small ||
                gridSize < BlockLimiterConfig.Instance.MaxBlocksSmallGrid) return false;
            count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlocksSmallGrid);



            return  true;

        }

        public static bool CountViolation(MyCubeBlockDefinition block, long owner)
        {
            return CountViolation(block.CubeSize, owner);
        }

        public static bool CountViolation(MyObjectBuilder_CubeGrid grid, long owner)
        {
            return CountViolation(grid.GridSizeEnum, owner);
        }

        public static bool CountViolation(MyCubeGrid grid, long owner)
        {
            return CountViolation(grid.GridSizeEnum, owner);
        }

        private static bool CountViolation(MyCubeSize size, long owner)
        {
            if (owner == 0) return false;
            if (Utilities.IsExcepted(owner)) return false;
            var playerGrids = new HashSet<MyCubeGrid>();
            GridCache.GetPlayerGrids(playerGrids,owner);
            var smallGrids = playerGrids.Count(x => x.GridSizeEnum == MyCubeSize.Small && IsBiggestGridInGroup(x));

            var largeGrids = playerGrids.Count(x => x.GridSizeEnum == MyCubeSize.Large && IsBiggestGridInGroup(x));
            if (size == MyCubeSize.Large)
            {
                if (BlockLimiterConfig.Instance.MaxLargeGrids == 0) return false;
                if (BlockLimiterConfig.Instance.MaxLargeGrids < 0) return true;
                return largeGrids >= BlockLimiterConfig.Instance.MaxLargeGrids;
            }

            if (BlockLimiterConfig.Instance.MaxSmallGrids == 0) return false;

            if (BlockLimiterConfig.Instance.MaxSmallGrids < 0) return true;
            return smallGrids >= BlockLimiterConfig.Instance.MaxSmallGrids;

        }

        public static bool IsBiggestGridInGroup(MyCubeGrid grid)
        {
            if (grid == null) return true;
            var biggestGrid = GetBiggestGridInGroup(grid);
            if (biggestGrid == null) return false;
            return grid == biggestGrid;
        }

        public static MyCubeGrid GetBiggestGridInGroup(MyCubeGrid grid)
        {
            if (grid == null) return null;
            var biggestGrid = grid;
            double num = 0.0;
            var nodes = MyCubeGridGroups.Static.Mechanical
                .GetGroup(grid)?.Nodes;
            if (nodes != null)
                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in nodes)
                {
                    double volume = node.NodeData.PositionComp.WorldAABB.Size.Volume;
                    if (volume > num)
                    {
                        num = volume;
                        biggestGrid = node.NodeData;
                    }
                }

            return biggestGrid;
        }

        public static List<MyCubeGrid> GetSubGrids(MyCubeGrid grid)
        {
            if (grid == null) return null;
            var result = new List<MyCubeGrid>();
            double num = 0.0;
            var nodes = MyCubeGridGroups.Static.Mechanical
                .GetGroup(grid)?.Nodes;
            if (nodes == null) return result;
            foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in nodes)
            {
                if (node.NodeData == grid) continue;
                result.Add(node.NodeData);
            }

            return result;
        }

        public static HashSet<MySlimBlock> GetSubGridBlocks(MyCubeGrid grid)
        {
            if (grid == null) return null;
            var result = new HashSet<MySlimBlock>();
            double num = 0.0;
            var nodes = MyCubeGridGroups.Static.Mechanical
                .GetGroup(grid)?.Nodes;
            if (nodes == null) return result;
            foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node node in nodes)
            {
                result.UnionWith(node.NodeData.CubeBlocks);
            }

            return result;
        }

        public static bool CanMerge(MyCubeGrid grid1, MyCubeGrid grid2)
        {
            return CanMerge(grid1, grid2, out _, out _, out _);
        }

        public static bool CanMerge(MyCubeGrid grid1, MyCubeGrid grid2, out List<string>blocks, out int count, out string limitName)
        {
            limitName = null;
            blocks = new List<string>();
            count = 0;
            if (grid1 == null || grid2 == null) return true;

            if (Utilities.IsExcepted(grid1) ||
                Utilities.IsExcepted(grid2)) return true;
            
            var blocksHash = new HashSet<MySlimBlock>(grid1.CubeBlocks);
            //Adding all present blocks from grid.
            blocksHash.UnionWith(grid2.CubeBlocks);
            blocksHash.UnionWith(GetSubGridBlocks(grid1));
            blocksHash.UnionWith(GetSubGridBlocks(grid2));
            if (blocksHash.Count == 0) return true;
            
            blocks.Add("All blocks - Size Violation");
            var gridSize = grid1.CubeBlocks.Count + grid2.CubeBlocks.Count;
            var gridType = grid1.GridSizeEnum;
            var isStatic = grid1.IsStatic || grid2.IsStatic;

            if (BlockLimiterConfig.Instance.MaxBlockSizeShips > 0 && !isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeShips)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlockSizeShips);
                limitName = "MaxBlockSizeShips";
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlockSizeStations > 0 && isStatic && gridSize >= BlockLimiterConfig.Instance.MaxBlockSizeStations)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlockSizeStations);
                limitName = "MaxBlockSizeStations";

                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksLargeGrid > 0 && gridType == MyCubeSize.Large && gridSize >= BlockLimiterConfig.Instance.MaxBlocksLargeGrid)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlocksLargeGrid);
                limitName = "MaxBlocksLargeGrid";
                return  false;
            }

            if (BlockLimiterConfig.Instance.MaxBlocksSmallGrid > 0 && gridType == MyCubeSize.Small && gridSize >= BlockLimiterConfig.Instance.MaxBlocksSmallGrid)
            {
                count = Math.Abs(gridSize - BlockLimiterConfig.Instance.MaxBlocksSmallGrid);
                limitName = "MaxBlocksSmallGrid";

                return  false;
            }
            blocks.Clear();
            count = 0;
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                limitName = limit.Name;
                if (!limit.LimitGrids) continue;

                if (limit.IsExcepted(grid1) || limit.IsExcepted(grid2)) continue;

                var matchingBlocks = new List<MySlimBlock>(blocksHash.Where(x=> limit.IsMatch(x.BlockDefinition)));
                
                if (matchingBlocks.Count <= limit.Limit) continue;
                count = Math.Abs(matchingBlocks.Count - limit.Limit);
                blocks.Add(matchingBlocks[0].BlockDefinition.ToString().Substring(16));

                return false;
            }

            return true;

        }

        public static bool AllowConversion(MyCubeGrid grid, out List<string> blocks, out int count, out string limitName )
        {
            limitName = null;
            blocks = new List<string> {"All blocks - Size Violation"};
            if (IsSizeViolation(grid,true, out count)) return false;

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (limit.IsExcepted(grid)) continue;

                limitName = limit.Name;
                if (!limit.LimitGrids) continue;
                switch (limit.GridTypeBlock)
                {
                    case LimitItem.GridType.ShipsOnly when !grid.IsStatic:
                    case LimitItem.GridType.StationsOnly when grid.IsStatic:
                    case LimitItem.GridType.AllGrids:
                    case LimitItem.GridType.SmallGridsOnly:
                    case LimitItem.GridType.LargeGridsOnly:
                        continue;
                }
                

                var matchingBlocks = new List<MySlimBlock>(grid.CubeBlocks.Where(x => limit.IsMatch(x.BlockDefinition)));

                if (matchingBlocks.Count <= limit.Limit)
                {
                    continue;
                }

                count = Math.Abs(matchingBlocks.Count - limit.Limit);
                return false;

            }
            limitName = null;
            return true;
        }
        
        public static bool CanSpawn(MyObjectBuilder_CubeGrid grid, long playerId)
        {
            if (Utilities.IsExcepted(playerId)) return true;

            if (grid == null || IsSizeViolation(grid)) return false;

            
            return playerId == 0 || Block.CanAdd(grid.CubeBlocks, playerId, out _);
        }

    }
}