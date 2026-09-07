import { createBrowserRouter } from 'react-router-dom'
import { PermissionsPage } from '../features/admin/pages/PermissionsPage'
import { RolesPage } from '../features/admin/pages/RolesPage'
import { UsersPage } from '../features/admin/pages/UsersPage'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { RegisterPage } from '../features/auth/pages/RegisterPage'
import { CasoDetailPage } from '../features/casos/pages/CasoDetailPage'
import { CasosListPage } from '../features/casos/pages/CasosListPage'
import { DashboardPage } from '../features/casos/pages/DashboardPage'
import { NuevoCasoPage } from '../features/casos/pages/NuevoCasoPage'
import { TiposDocumentoPage } from '../features/casos/pages/TiposDocumentoPage'
import { FlujoDetailPage } from '../features/flujos/pages/FlujoDetailPage'
import { FlujosListPage } from '../features/flujos/pages/FlujosListPage'
import { StorageConfigsPage } from '../features/flujos/pages/StorageConfigsPage'
import { CompaniesPage } from '../features/markets/pages/CompaniesPage'
import { CompanyDetailPage } from '../features/markets/pages/CompanyDetailPage'
import { TrabajoDetailPage } from '../features/ops/pages/TrabajoDetailPage'
import { TrabajosPage } from '../features/ops/pages/TrabajosPage'
import { ProfilePage } from '../features/profile/pages/ProfilePage'
import { DespliguesPage } from '../features/rpa/pages/DespliguesPage'
import { EquiposPage } from '../features/rpa/pages/EquiposPage'
import { ServiciosPage } from '../features/rpa/pages/ServiciosPage'
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
      {
        path: 'mercados',
        element: (
          <ProtectedRoute>
            <CompaniesPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'mercados/:ticker',
        element: (
          <ProtectedRoute>
            <CompanyDetailPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/flujos',
        element: (
          <ProtectedRoute>
            <FlujosListPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/flujos/:id',
        element: (
          <ProtectedRoute>
            <FlujoDetailPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/almacenamiento',
        element: (
          <ProtectedRoute>
            <StorageConfigsPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/tipos-documento',
        element: (
          <ProtectedRoute>
            <TiposDocumentoPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/rpa/equipos',
        element: (
          <ProtectedRoute>
            <EquiposPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/rpa/servicios',
        element: (
          <ProtectedRoute>
            <ServiciosPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'admin/rpa/despliegues',
        element: (
          <ProtectedRoute>
            <DespliguesPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'casos',
        element: (
          <ProtectedRoute>
            <DashboardPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'casos/lista',
        element: (
          <ProtectedRoute>
            <CasosListPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'casos/nuevo',
        element: (
          <ProtectedRoute>
            <NuevoCasoPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'casos/:id',
        element: (
          <ProtectedRoute>
            <CasoDetailPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'ops/trabajos',
        element: (
          <ProtectedRoute>
            <TrabajosPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'ops/trabajos/:id',
        element: (
          <ProtectedRoute>
            <TrabajoDetailPage />
          </ProtectedRoute>
        ),
      },
    ],
  },
])
