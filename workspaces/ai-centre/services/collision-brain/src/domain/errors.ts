export class AppError extends Error {
  constructor(
    message: string,
    readonly code: string,
    readonly statusCode: number,
  ) {
    super(message);
    this.name = new.target.name;
  }
}

export class ValidationError extends AppError {
  constructor(message: string) {
    super(message, "validation_error", 400);
  }
}

export class UnauthorizedError extends AppError {
  constructor(message = "Authentication is required") {
    super(message, "unauthorized", 401);
  }
}

export class ForbiddenError extends AppError {
  constructor(message = "The authenticated user does not have the required role") {
    super(message, "forbidden", 403);
  }
}

export class NotFoundError extends AppError {
  constructor(message: string) {
    super(message, "not_found", 404);
  }
}

export class ConflictError extends AppError {
  constructor(message: string) {
    super(message, "conflict", 409);
  }
}
