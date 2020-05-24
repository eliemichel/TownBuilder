
namespace LilyWfc
{

    /**
     * Connection between two variables (ie vertices) in the Wave Function System
     */
    public class WaveConnection
    {
        public int type; // symbol labeling this connection, e.g. North, South, East, West
        public WaveVariable destination;
    }

}
