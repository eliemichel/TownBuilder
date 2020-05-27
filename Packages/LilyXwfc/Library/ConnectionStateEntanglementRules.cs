using UnityEngine;

namespace LilyXwfc
{

    /**
     * The most common type of entanglement rules is the "connection state" model.
     * A module imposes a state to its outgoing connections. For instance, a module
     * representing a vertical straight road will impose that its left and right
     * connection have state NoRoad while its top and down connections have state
     * Road. It can only be connected to module such that
     *     ConnectionState(otherModule, DualConnection(c)) == ConnectionState(thisModule, c)
     * To explore: In this cas, it might be better to propagate collapse over the
     * set of connection states rather than module states.
     */
    public class ConnectionStateEntanglementRules : AEntanglementRules
    {
        readonly ModuleRegistry registry;

        public ConnectionStateEntanglementRules(ModuleRegistry registry)
        {
            this.registry = registry;
        }

        ConnectionState GetConnectionState(PureState x, int connectionType)
        {
            return registry.GetModule(x).GetConnectionState(connectionType);
        }

        public override bool Allows(PureState x, BMesh.Loop connection, PureState y)
        {
            int connectionType = connection.attributes["adjacency"].asInt().data[0];
            int dualConnectionType = connection.next.attributes["adjacency"].asInt().data[0];
            ConnectionState s1 = GetConnectionState(x, connectionType);
            ConnectionState s2 = GetConnectionState(y, dualConnectionType);
            return s1.Equals(s2);
        }

        public override SuperposedState AllowedStates(SuperposedState x, BMesh.Loop connection, SuperposedState y)
        {
            SuperposedState z = SuperposedState.None(y);
            int connectionType = connection.attributes["adjacency"].asInt().data[0];
            int dualConnectionType = connection.next.attributes["adjacency"].asInt().data[0];
            var components = x.Components();
            if (components.Count == 0) return z;

            ConnectionState[] cache = new ConnectionState[z.DimensionInExclusionClass];
            {
                int k = 0;
                foreach (var zc in z.NonExcludedPureStates())
                {
                    cache[k] = GetConnectionState(zc, dualConnectionType);
                    ++k;
                }
            }

            foreach (PureState xc in components)
            {
                ConnectionState s1 = registry.GetModule(xc).GetConnectionState(connectionType);

                int k = 0;
                foreach (var zc in z.NonExcludedPureStates())
                {
                    if (s1.Equals(cache[k]))
                    {
                        z.Add(zc);
                    }
                    ++k;
                }
            }
            return z;
        }
    }

}
