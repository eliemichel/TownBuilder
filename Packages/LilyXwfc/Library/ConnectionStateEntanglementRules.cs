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
        readonly int[] connectionLut;

        public ConnectionStateEntanglementRules(ModuleRegistry registry, int[] connectionLut = null)
        {
            if (connectionLut == null)
            {
                connectionLut = new int[] { 1, 0, 3, 2 }; // default for 2d rectangle tiles
            }

            this.registry = registry;
            this.connectionLut = connectionLut;
        }

        ConnectionState GetConnectionState(PureState x, int connectionType)
        {
            return registry.GetModule(x).GetConnectionState(connectionType);
        }

        public override bool Allows(PureState x, int connectionType, PureState y)
        {
            ConnectionState s1 = GetConnectionState(x, connectionType);
            ConnectionState s2 = GetConnectionState(y, DualConnection(connectionType));
            return s1.Equals(s2);
        }

        public override SuperposedState AllowedStates(SuperposedState x, int connectionType, SuperposedState y)
        {
            SuperposedState z = SuperposedState.None(y);
            int dualConnectionType = DualConnection(connectionType);
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

        /**
         * If edge is stored going from other vertex to this, we use the dual connection type
         * e.g. is the edge means "other is on the right of vert" we translate into "vert is on the left of other"
         */
        public override int DualConnection(int c)
        {
            return connectionLut[c];
        }
    }

}
