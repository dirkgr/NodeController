namespace NodeController {
    using System;
    using System.Collections.Generic;
    using Util;

    [Serializable]
    public class NodeManager {
        #region LifeCycle
        public static NodeManager Instance { get; private set; } = new NodeManager();

        public static byte[] Serialize() => SerializationUtil.Serialize(Instance);

        public static void Deserialize(byte[] data) {
            if (data == null) {
                Instance = new NodeManager();
                Log.Debug($"NodeManager.Deserialize(data=null)");

            } else {
                Log.Debug($"NodeManager.Deserialize(data): data.Length={data?.Length}");
                Instance = SerializationUtil.Deserialize(data) as NodeManager;
            }
        }

        public void OnLoad() {
            RefreshAllNodes();
        }

        #endregion LifeCycle

        public NodeData[] buffer = new NodeData[NetManager.MAX_NODE_COUNT];

        public List<NodeData> GetNodeDataList() {
            var ret = new List<NodeData>();
            foreach(NodeData nodeData in buffer){
                if (nodeData != null)
                    ret.Add(nodeData);
            }
            return ret;
        }

        #region data tranfer
        public static byte[] CopyNodeData(ushort nodeID) =>
            Instance.CopyNodeDataImp(nodeID);

        public static void PasteNodeData(ushort nodeID, byte[] data) =>
            Instance.PasteNodeDataImp(nodeID, data);

        /// <summary>
        /// clones node data before transfering it to newNodeID
        /// </summary>
        public void TransferNodeData(ushort newNodeID, NodeData nodedata, bool refresh=true) {
            Log.Debug($"transfering {nodedata} to {newNodeID}");
            buffer[newNodeID] = nodedata.Clone();
            buffer[newNodeID].NodeID = newNodeID;
            
            if(refresh)
                RefreshData(newNodeID);
        }

        private byte[] CopyNodeDataImp(ushort nodeID) {
            var nodeData = buffer[nodeID];
            if (nodeData == null) {
                Log.Debug($"node:{nodeID} has no custom data");
                return null;
            }
            return SerializationUtil.Serialize(nodeData);
        }

        private void PasteNodeDataImp(ushort nodeID, byte[] data) {
            if (data == null) {
                ResetNodeToDefault(nodeID);
            } else {
                buffer[nodeID] = SerializationUtil.Deserialize(data) as NodeData;
                buffer[nodeID].NodeID = nodeID;
                RefreshData(nodeID);
            }
        }
        #endregion

        public NodeData InsertNode(NetTool.ControlPoint controlPoint, NodeTypeT nodeType = NodeTypeT.Crossing) {
            if(ToolBase.ToolErrors.None != NetUtil.InsertNode(controlPoint, out ushort nodeID))
                return null;
            HelpersExtensions.Assert(nodeID!=0,"nodeID");

            int nPedLanes = controlPoint.m_segment.ToSegment().Info.CountPedestrianLanes();
            if (nodeType == NodeTypeT.Crossing && nPedLanes<2)
                buffer[nodeID] = new NodeData(nodeID);
            else
                buffer[nodeID] = new NodeData(nodeID, nodeType);
            return buffer[nodeID];
        }

        public NodeData GetOrCreate(ushort nodeID) {
            NodeData data = Instance.buffer[nodeID];
            if (data == null) {
                data = new NodeData(nodeID);
                buffer[nodeID] = data;
            }
            return data;
        }

        /// <summary>
        /// releases data for <paramref name="nodeID"/> if uncessary. Calls update node.
        /// </summary>
        /// <param name="nodeID"></param>
        public void RefreshData(ushort nodeID) {
            if (nodeID == 0 || buffer[nodeID] == null)
                return;
            if (buffer[nodeID].IsDefault()) {
                ResetNodeToDefault(nodeID);
            } else {
                buffer[nodeID].Refresh();
            }
        }

        public void ResetNodeToDefault(ushort nodeID) {
            if(buffer[nodeID]!=null)
                Log.Debug($"node:{nodeID} reset to defualt");
            else
                Log.Debug($"node:{nodeID} is alreadey null. no ne");
            buffer[nodeID] = null;
            NetManager.instance.UpdateNode(nodeID);
        }

        public void RefreshAllNodes() {
            foreach (var nodeData in buffer)
                nodeData?.Refresh();
        }

        public void OnBeforeCalculateNode(ushort nodeID) {
            // nodeID.ToNode still has default flags.
            if (buffer[nodeID] == null)
                return;
            if (!NodeData.IsSupported(nodeID)) {
                buffer[nodeID] = null;
                return;
            }

            buffer[nodeID].Calculate();

            if (!buffer[nodeID].CanChangeTo(buffer[nodeID].NodeType)) {
                buffer[nodeID] = null;
            }
        }

        //public void ChangeNode(ushort nodeID) {
        //    Log.Info($"ChangeNode({nodeID}) called");
        //    NodeData data = GetOrCreate(nodeID);
        //    data.ChangeNodeType();
        //    Instance.buffer[nodeID] = data;
        //    RefreshData(nodeID);
        //}
    }
}
