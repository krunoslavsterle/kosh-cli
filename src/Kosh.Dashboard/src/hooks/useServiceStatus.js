import { useEffect, useState } from "react";
import { serviceStore } from "../state/serviceStore";

export function useServiceStatus() {
  const [data, setData] = useState(serviceStore.data);

  useEffect(() => {
    return serviceStore.subscribe(setData);
  }, []);

  return data;
}
