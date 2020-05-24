namespace LilyXwfc
{
    /**
     * Entanglement rules define the interaction between neighboring variables
     * in a WaveFunctionSystem.
     */
    public interface IEntanglementRules
    {
        /**
         * Return the probability that two variables X and Y are respectively
         * in states x and y knowing that they are related by a connection of
         * type #connectionType.
         */
        bool Allows(PureState x, int connectionType, PureState y);

        /**
         * Contrary to regular WFC, the superposition must depend on y because
         * a single SuperposedState object cannot hold all the allowed neighbors
         */
        SuperposedState AllowedStates(SuperposedState x, int connectionType, SuperposedState y);

        /**
         * If edge is stored going from other vertex to this, we use the dual connection type
         * e.g. is the edge means "other is on the right of vert" we translate
         * into "vert is on the left of other"
         */
        int DualConnection(int c);

        /**
         * The number of dimensions within a given class may be inferior to the
         * maximum number of dimensions provided to the system (this is used by
         * EquiprobableInClass for instance). Return -1 to mean maximum.
         */
        int DimensionInExclusionClass(int exclusionClass);
    }

    /**
     * Provide default implementation of optional methods of IEntanglementRules
     */
    public abstract class AEntanglementRules : IEntanglementRules
    {
        public abstract bool Allows(PureState x, int connectionType, PureState y);
        public abstract int DualConnection(int c);

        public virtual SuperposedState AllowedStates(SuperposedState x, int connectionType, SuperposedState y)
        {
            SuperposedState z = SuperposedState.None(y);
            foreach (PureState xc in x.Components())
            {
                foreach (PureState zc in z.NonExcludedPureStates())
                {
                    if (Allows(xc, connectionType, zc))
                    {
                        z.Add(zc);
                    }
                }
            }
            return z;
        }

        public virtual int DimensionInExclusionClass(int exclusionClass)
        {
            return -1;
        }
    }
}
