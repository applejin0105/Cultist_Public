using Domain.Enums;
using Domain.State.Host;
using Domain.Structure.Field;

namespace Systems
{
    /// <summary>
    /// 필드 관련 로직을 수행하는 System 클래스
    /// </summary>
    public class FieldSystem
    {
        public static void PlaceAsStartCard(GameState state, int rootCardInstanceId)
        {
            var card = state.GetCard(rootCardInstanceId);
            if (card == null) return;

            Player player = card.OwnerSeat;

            var playerFieldTree = state.GetFieldTreeById(player);
            if (!playerFieldTree.Nodes.TryGetValue(rootCardInstanceId, out var node))
            {
                node = new FieldNode(rootCardInstanceId);
                playerFieldTree.Nodes[rootCardInstanceId] = node;
            }

            node.SetParent(null);
            CardMovementSystem.MoveCard(state, rootCardInstanceId, player, Zone.Field, CardStatus.FieldFront);
        }

        public static void PlaceAsNewCard(GameState state, Player player, int parentInstanceId, int instanceId)
        {
            var playerFieldTree = state.GetFieldTreeById(player);

            if (!playerFieldTree.Nodes.TryGetValue(parentInstanceId, out var parentNode))
            {
                parentNode = new FieldNode(parentInstanceId, null);
                playerFieldTree.Nodes[parentInstanceId] = parentNode;
            }

            var childNode = new FieldNode(instanceId, parentInstanceId);
            playerFieldTree.Nodes[instanceId] = childNode;

            parentNode.AddChild(instanceId);

            var card = state.GetCard(instanceId);
            if (card != null)
            {
                CardMovementSystem.MoveCard(state, instanceId, player, Zone.Field, CardStatus.FieldBack);
            }
        }
    }
}