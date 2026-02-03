import { Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, RequireAuth } from "./components/AuthProvider";
import Layout from "./components/Layout";
import Dashboard from "./pages/Dashboard";
import Home from "./pages/Home";
import Login from "./pages/Login";
import Settings from "./pages/Settings";
import SummaryDetail from "./pages/SummaryDetail";

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/" element={<Navigate to="/app" replace />} />
        <Route
          path="/app"
          element={
            <RequireAuth>
              <Layout>
                <Home />
              </Layout>
            </RequireAuth>
          }
        />
        <Route
          path="/app/dashboard"
          element={
            <RequireAuth>
              <Layout>
                <Dashboard />
              </Layout>
            </RequireAuth>
          }
        />
        <Route
          path="/app/summaries/:summaryId"
          element={
            <RequireAuth>
              <Layout>
                <SummaryDetail />
              </Layout>
            </RequireAuth>
          }
        />
        <Route
          path="/app/settings"
          element={
            <RequireAuth>
              <Layout>
                <Settings />
              </Layout>
            </RequireAuth>
          }
        />
      </Routes>
    </AuthProvider>
  );
}
