import { createBrowserRouter } from 'react-router-dom'
import { PermissionsPage } from '../features/admin/pages/PermissionsPage'
import { RolesPage } from '../features/admin/pages/RolesPage'
import { UsersPage } from '../features/admin/pages/UsersPage'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { RegisterPage } from '../features/auth/pages/RegisterPage'
import { ProfilePage } from '../features/profile/pages/ProfilePage'
import { AppLayout } from './AppLayout'
import { HomePage } from './HomePage'
import { ProtectedRoute } from './ProtectedRoute'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      {
        index: true,
        element: (
          <ProtectedRoute>
            <HomePage />
          </ProtectedRoute>
        ),
      },
      { path: 'login', element: <LoginPage /> },
      { path: 'registro', element: <RegisterPage /> },
      {
        path: 'perfil',
        element: (
          <ProtectedRoute>
            <ProfilePage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/usuarios',
        element: (
          <ProtectedRoute>
            <UsersPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/roles',
        element: (
          <ProtectedRoute>
            <RolesPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/permisos',
        element: (
          <ProtectedRoute>
            <PermissionsPage />
          </ProtectedRoute>
        ),
      },
    ],
  },
])
