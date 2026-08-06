# 🏥 Clinic Management API

A production-oriented RESTful API for managing clinics, doctors, patients, appointments, medical records, prescriptions, and payments.

Built with ASP.NET Core Web API following a Three-Tier Architecture with a strong focus on security, maintainability, and clean architecture.

## 🚀 Features

- JWT Authentication
- Role-Based Authorization
- Resource Ownership Authorization
- Refresh Token Authentication
- Rate Limiting
- Auditing
- Logging
- SQL Server Database
- Stored Procedures
- DTO Pattern
- Three-Tier Architecture
- Centralized Error Handling
- SQL Server Stored Procedures
- SQL-Based Business Validation

## 🛠 Technologies

- HTTPS
- CORS
- ASP.NET Core Web API
- C#
- SQL Server
- ADO.NET
- JWT Bearer Authentication
- BCrypt
- Swagger / OpenAPI

## 🔐 Security

This project implements multiple security mechanisms including:

- HTTPS Communication
- CORS Policy Configuration
- JWT Access Tokens
- Refresh Token Rotation
- Role-Based Authorization
- Resource Ownership Authorization
- Password Hashing using BCrypt
- Rate Limiting
- Audit Logging

## 📋 Auditing

The API records important business operations into dedicated Audit tables.

Examples include:

- Creating records
- Updating records
- User who performed the action
- Operation timestamp

Audit information is stored through the Business Layer and Data Access Layer to keep controllers clean and maintainable.

## 📝 Logging

The application records important system events and business operations into SQL Server logging tables.

Logging is implemented through the Business Layer and Data Access Layer, keeping controllers focused on request handling.

## ⚡ Performance

Business-critical operations are implemented using SQL Server Stored Procedures to reduce database round trips and improve performance.

Database-side validation is also used where appropriate to keep the API lightweight and efficient.

## ❗ Error Handling

The project uses a database-driven error handling approach.

- Business validation is performed inside SQL Server Stored Procedures.
- SQL Server raises meaningful business exceptions.
- The Data Access Layer captures and propagates SQL exceptions.
- Controllers translate these exceptions into appropriate HTTP Status Codes and API responses.

This approach keeps business rules centralized in the database while maintaining clean and consistent API endpoints.

## 📁 Architecture

```
Controllers
      ↓
Business Layer
      ↓
Data Access Layer
      ↓
SQL Server
```

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server
- Visual Studio 2022 or later

### Setup

1. Clone the repository.

```bash
git clone https://github.com/mohamedimam-dev/Clinic-Management-Api.git
```

2. Configure the `appsettings.json` file.

3. Create the Clinic Management database.

4. Execute the SQL scripts to create the database objects.

5. Run the project.

6. Open Swagger to test the API.


## 📄 License

This project is for learning and portfolio purposes.
