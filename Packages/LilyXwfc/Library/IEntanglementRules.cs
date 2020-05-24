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
         * Return a superposition of all states that are possible for Y if X is
         * in superposed state x and X and Y are bound by a connection of type
         * #connectionType.
         * 
         * Implementing this is optional, a default implementation is provided
         * if you derive from AEntanglementRules defined bellow, but you can
         * choose to override it essentially for optimization. The behavior
         * must not change.
         */
        SuperposedState AllowedStates(SuperposedState x, int connectionType);

        /**
         * If edge is stored going from other vertex to this, we use the dual connection type
         * e.g. is the edge means "other is on the right of vert" we translate
         * into "vert is on the left of other"
         */
        int DualConnection(int c);
    }

    /**
     * Provide default implementation of optional methods of IEntanglementRules
     */
    public abstract class AEntanglementRules : IEntanglementRules
    {
        public abstract bool Allows(PureState x, int connectionType, PureState y);
        public abstract int DualConnection(int c);

        public virtual SuperposedState AllowedStates(SuperposedState x, int connectionType)
        {
            SuperposedState y = SuperposedState.None(x.Dimension);
            foreach (PureState xc in x.Components())
            {
                for (int k = 0; k < x.Dimension; ++k)
                {
                    var yc = new PureState(k);
                    if (Allows(xc, connectionType, yc))
                    {
                        y.Add(yc);
                    }
                }
            }
            return y;
        }
    }
}
