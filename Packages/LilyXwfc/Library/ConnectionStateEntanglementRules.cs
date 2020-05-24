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

        public override SuperposedState AllowedStates(SuperposedState x, int connectionType)
        {
            SuperposedState y = SuperposedState.None(x.Dimension);
            int dualConnectionType = DualConnection(connectionType);
            var components = x.Components();
            if (components.Count == 0) return y;

            ConnectionState[] cache = new ConnectionState[x.Dimension];
            for (int k = 0; k < cache.Length; ++k)
            {
                cache[k] = GetConnectionState(new PureState(k), dualConnectionType);
            }

            foreach (PureState xc in components)
            {
                ConnectionState s1 = registry.GetModule(xc).GetConnectionState(connectionType);

                for (int k = 0; k < x.Dimension; ++k)
                {
                    if (s1.Equals(cache[k]))
                    {
                        y.Add(new PureState(k));
                    }
                }
            }
            return y;
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
