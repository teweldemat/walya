import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#1f8ecd'
    },
    secondary: {
      main: '#f97316'
    },
    background: {
      default: '#0f172a',
      paper: '#111827'
    }
  },
  typography: {
    fontFamily: 'Inter, "Roboto", "Helvetica", "Arial", sans-serif',
    h4: {
      fontWeight: 600
    }
  }
});
