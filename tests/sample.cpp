#define VERSION "1.0"

/**
 * A simple vector class.
 */
class Vector {
public:
    Vector(double x, double y);

    /** Get the magnitude. */
    double magnitude() const;

    double x_, y_;
};

struct Config {
    int width;
    int height;
};

enum class Direction {
    North,
    South,
    East,
    West
};

using Size = std::pair<int, int>;

double dot_product(const Vector& a, const Vector& b) {
    return a.x_ * b.x_ + a.y_ * b.y_;
}
