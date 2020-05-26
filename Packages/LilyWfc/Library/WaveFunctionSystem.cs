using System.Collections.Generic;
using UnityEditorInternal;

namespace LilyWfc
{

    /**
     * The "system" (in the physical acceptance of this word) is made of the
     * connection topology (typically a grid) and the state vector, that assigns a
     * wave function (ie a superposed state) to each vertex of the topology.
     */
    public class WaveFunctionSystem
    {
        public readonly int dimension; // number of possible states (i.e. dimension of the superposed state space)
        SuperposedState[] waves; // state vector, one state per topology vertex
        public readonly BMesh topology;
        public readonly IEntanglementRules rules;

        public WaveFunctionSystem(BMesh topology, IEntanglementRules rules, int dimension)
        {
            this.dimension = dimension;
            { int i = 0; foreach (var v in topology.vertices) v.id = i++; } // ensure ids
            this.topology = topology;
            this.rules = rules;
            Reset();
        }

        /**
         * To add initial conditions, inherit from system and reimplement the reset
         */
        public virtual void Reset()
        {
            waves = new SuperposedState[topology.vertices.Count];
            for (int i = 0; i < waves.Length; ++i)
            {
                waves[i] = SuperposedState.Equiprobable(dimension);
            }
        }

        public void SetWave(WaveVariable idx, SuperposedState state)
        {
            waves[idx.Raw()] = state;
        }

        public SuperposedState GetWave(WaveVariable idx)
        {
            return waves[idx.Raw()];
        }

        /**
         * Get the list of all connection coming from a given vertex.
         * This is a wrapper around BMesh.Vertex.NeighborEdges that returns
         * edges transformed into WaveConnection objects.
         */
        public List<WaveConnection> OutgoingConnections(WaveVariable src)
        {
            var vert = topology.vertices[src.Raw()];

            List<WaveConnection> connections = new List<WaveConnection>();

            foreach (var e in vert.NeighborEdges())
            {
                var dest = e.OtherVertex(vert);
                int type = e.attributes["type"].asInt().data[0];
                if (vert != e.vert1)
                {
                    type = rules.DualConnection(type);
                }
                connections.Add(new WaveConnection { destination = WaveVariable.FromRaw(dest.id), type = type });
            }
            return connections;
        }

        /**
         * To be used like so: foreach (var x in system.Variables()) { ... }
         */
        public IEnumerable<WaveVariable> Variables()
        {
            foreach (var v in topology.vertices)
            {
                yield return WaveVariable.FromRaw(v.id);
            }
        }

        public int VariableCount { get { return waves.Length; } }
    }

}
