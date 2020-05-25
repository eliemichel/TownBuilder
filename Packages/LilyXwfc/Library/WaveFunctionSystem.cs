using System.Collections.Generic;
using System.Diagnostics;
using UnityEditorInternal;

namespace LilyXwfc
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
        readonly BMesh topology;
        public readonly IEntanglementRules rules;
        readonly string exclusionClassAttr;

        /**
         * @param exclusionClassAttr name of the vertex attribute in the
         * topology mesh that tells which exclusion class to initialize the
         * corresponding wave variable with. It must be an integer attribute.
         * If null, the 0th exclusion class will be used for all states, which
         * makes XWFC totally equivalent to WFC.
         */
        public WaveFunctionSystem(BMesh topology, IEntanglementRules rules, int dimension, string exclusionClassAttr = null)
        {
            this.dimension = dimension;
            { int i = 0; foreach (var v in topology.vertices) v.id = i++; } // ensure ids
            this.topology = topology;
            this.rules = rules;
            this.exclusionClassAttr = exclusionClassAttr;
            Reset();
        }

        /**
         * To add initial conditions, inherit from system and reimplement the reset.
         * With Exclusive WFC, it is mandatory to provide an exclusion class,
         * and we use a special vertex attribute for this.
         */
        public virtual void Reset()
        {
            waves = new SuperposedState[topology.vertices.Count];
            int i = 0;
            foreach (var v in topology.vertices)
            {
                int exclusionClass =
                    exclusionClassAttr != null
                    ? v.attributes[exclusionClassAttr].asInt().data[0]
                    : 0;
                waves[i] = SuperposedState.EquiprobableInClass(dimension, rules.DimensionInExclusionClass(exclusionClass), exclusionClass);
                ++i;
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

                if (type == 1 /*above*/) Debug.Assert(vert.point.y > e.OtherVertex(vert).point.y, vert.id + " is not above " + e.OtherVertex(vert).id);
                if (type == 2 /*bellow*/) Debug.Assert(vert.point.y < e.OtherVertex(vert).point.y, vert.id + " is not bellow " + e.OtherVertex(vert).id);

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
