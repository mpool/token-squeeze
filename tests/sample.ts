const API_URL = "https://api.example.com";

interface User {
    name: string;
    email: string;
}

type Status = "active" | "inactive";

enum Role {
    Admin,
    User,
    Guest
}

function createUser(name: string, email: string): User {
    return { name, email };
}

class UserService {
    /** Find a user by email. */
    findByEmail(email: string): User | undefined {
        return undefined;
    }

    deleteUser(id: number): void {}
}
