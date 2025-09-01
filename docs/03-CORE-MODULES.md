# Core Modules Documentation

This document provides detailed technical information about the core modules of the DriveOps repository. The core modules covered in this document are Users, Vehicles, Notifications, and File Management.

## 1. Users Module

### Overview
The Users module is responsible for managing user accounts, authentication, and authorization within the DriveOps application.

### Features
- **User Registration**: Allows new users to create an account.
- **User Authentication**: Supports login/logout functionalities and session management.
- **Profile Management**: Users can update their profile information and change passwords.

### Technical Details
- **Database Schema**: The Users module utilizes a relational database with a `users` table that includes fields such as `id`, `username`, `email`, `hashed_password`, and `created_at`.
- **API Endpoints**:
  - `POST /api/users/register`: Register a new user.
  - `POST /api/users/login`: Authenticate a user.
  - `GET /api/users/:id`: Retrieve user profile information.

## 2. Vehicles Module

### Overview
The Vehicles module manages the information related to vehicles in the DriveOps system.

### Features
- **Vehicle Registration**: Users can register new vehicles.
- **Vehicle Tracking**: Track the status and location of registered vehicles.
- **Maintenance Records**: Manage records of maintenance and repairs for each vehicle.

### Technical Details
- **Database Schema**: The Vehicles module has a `vehicles` table with fields such as `id`, `user_id`, `make`, `model`, `year`, and `status`.
- **API Endpoints**:
  - `POST /api/vehicles`: Register a new vehicle.
  - `GET /api/vehicles/:id`: Get details of a specific vehicle.
  - `PUT /api/vehicles/:id`: Update vehicle information.

## 3. Notifications Module

### Overview
The Notifications module handles the sending and management of notifications within the application.

### Features
- **Real-time Notifications**: Users receive real-time alerts about important events.
- **Notification History**: Users can view their past notifications.

### Technical Details
- **Database Schema**: The Notifications module includes a `notifications` table with fields like `id`, `user_id`, `message`, `type`, `is_read`, and `created_at`.
- **API Endpoints**:
  - `GET /api/notifications`: Retrieve a list of notifications for the logged-in user.
  - `POST /api/notifications`: Create a new notification.

## 4. File Management Module

### Overview
The File Management module provides functionality for handling files related to users and vehicles.

### Features
- **File Upload**: Users can upload documents and images.
- **File Retrieval**: Retrieve files associated with users or vehicles.
- **File Deletion**: Users can delete uploaded files.

### Technical Details
- **Storage**: The File Management module uses cloud storage (e.g., AWS S3) for storing files.
- **API Endpoints**:
  - `POST /api/files/upload`: Upload a new file.
  - `GET /api/files/:id`: Retrieve a specific file.
  - `DELETE /api/files/:id`: Delete a file.

## Conclusion

This document serves as a comprehensive guide to the core modules in the DriveOps system. Each module plays a crucial role in providing functionality and enhancing the user experience within the application.
